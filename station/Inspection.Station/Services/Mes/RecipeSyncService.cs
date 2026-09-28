using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inspection.Contracts;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Services.Mes;

/// <summary>레시피 동기화 한 번의 결과. GET /api/recipes/status로 보여 준다.</summary>
public record RecipeSyncResult(
    DateTimeOffset At,
    List<string> Updated,                 // 새로 받아 파일을 바꾼 제품
    List<string> Skipped,                 // 이미 같은 recipeId라 건너뜀
    Dictionary<string, string> Failed,    // 제품(또는 "(목록)") → 실패 이유. 이 제품은 기존 파일 그대로
    string? Error);                       // 동기화 전체가 못 돈 이유 (목록 조회 실패 등)

/// <summary>
/// 로그인 때 MES의 활성 레시피를 받아 Recipes/{productId}.json을 갱신한다.
///
///   1. GET {RecipesPath} → active=true만, 제품당 version 최대 하나
///   2. recipes-meta.json의 recipeId와 같고 파일이 있으면 건너뜀
///   3. GET {RecipesPath}/{recipeId} → 스테이션 레시피 형식으로 변환 → 검증
///      (부품 1개 이상, 부품마다 홀 1개 이상, 홀 형식 [A-U](0~13)·실재하는 홀, 모델 클래스 이름)
///   4. 기존 파일을 Recipes/backup/{productId}.v{이전 버전}.json으로 복사 → 임시 파일에 쓰고 이름 바꾸기
///   5. 메타 갱신, 레시피 캐시 무효화
///
/// 어느 단계에서 실패해도 그 제품의 기존 파일은 건드리지 않는다 → 검사는 로컬 레시피로 계속된다.
/// </summary>
public class RecipeSyncService
{
    /// <summary>모델(yolov8n)이 내는 클래스 이름. 레시피에 이 밖의 이름이 있으면 검사가 늘 "다른 부품"이 되므로 받지 않는다.</summary>
    public static readonly IReadOnlySet<string> ValidClasses = new HashSet<string>
    {
        "black_wire", "capacitor", "ic_chip", "led",
        "red_wire", "resistance330", "resistance470", "white_wire",
    };

    private static readonly Regex HolePattern = new("^[A-U](?:[0-9]|1[0-3])$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions ReadJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions WriteJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // note의 한글을 그대로 읽히게
    };

    private readonly Func<HttpClient> _http;
    private readonly MesOptions _options;
    private readonly RecipeFolder _folder;
    private readonly RecipeMetaStore _meta;
    private readonly IRecipeProvider _recipes;
    private readonly ILogger<RecipeSyncService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);   // 두 사람이 동시에 로그인해도 한 번에 하나만

    public RecipeSyncService(Func<HttpClient> http, IOptions<MesOptions> options, RecipeFolder folder,
        RecipeMetaStore meta, IRecipeProvider recipes, ILogger<RecipeSyncService> log)
    {
        _http = http;
        _options = options.Value;
        _folder = folder;
        _meta = meta;
        _recipes = recipes;
        _log = log;
    }

    public RecipeSyncResult? LastResult { get; private set; }

    public async Task<RecipeSyncResult> SyncAsync(string accessToken, CancellationToken ct)
    {
        var result = new RecipeSyncResult(DateTimeOffset.Now, new(), new(), new(), null);
        if (!await _gate.WaitAsync(0, ct))
            return result with { Error = "다른 동기화가 진행 중이라 건너뜀" };

        try
        {
            using var http = _http();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            List<MesRecipeSummary> list;
            try
            {
                list = await http.GetFromJsonAsync<List<MesRecipeSummary>>(_options.RecipesPath, ReadJson, ct) ?? new();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _log.LogWarning("MES 레시피 목록 조회 실패 → 로컬 레시피 유지: {Message}", ex.Message);
                return LastResult = result with { Error = $"목록 조회 실패: {ex.Message}" };
            }

            // 제품마다 활성 레시피 하나 (여러 개면 가장 높은 버전)
            var targets = list.Where(r => r.Active && !string.IsNullOrWhiteSpace(r.ProductId))
                              .GroupBy(r => r.ProductId)
                              .Select(g => g.OrderByDescending(r => r.Version).First());

            foreach (var summary in targets)
            {
                ct.ThrowIfCancellationRequested();
                string productId = summary.ProductId;
                try
                {
                    string path = RecipePath(productId);
                    if (_meta.Get(productId)?.RecipeId == summary.RecipeId && File.Exists(path))
                    {
                        result.Skipped.Add(productId);
                        continue;
                    }

                    var detail = await http.GetFromJsonAsync<MesRecipeDetail>(
                                     $"{_options.RecipesPath}/{summary.RecipeId}", ReadJson, ct)
                                 ?? throw new InvalidDataException("레시피 상세가 비어 있음");

                    var file = ToStationRecipe(detail);
                    string? problem = Validate(file, productId);
                    if (problem is not null) throw new InvalidDataException(problem);

                    Save(productId, file);
                    _meta.Set(productId, new RecipeMeta(detail.RecipeId, detail.Version, DateTimeOffset.Now));
                    _recipes.Invalidate(productId);
                    result.Updated.Add(productId);
                    _log.LogInformation("MES 레시피 적용: {Product} v{Version} ({RecipeId})",
                        productId, detail.Version, detail.RecipeId);
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or IOException)
                {
                    result.Failed[productId] = ex.Message;
                    _log.LogWarning("MES 레시피 {Product} 적용 실패 → 기존 파일 유지: {Message}", productId, ex.Message);
                }
            }

            return LastResult = result;
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("MES 레시피 동기화 시간 초과 → 끝난 제품까지만 적용, 나머지는 기존 파일 유지");
            return LastResult = result with { Error = "시간 초과" };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>MES 상세 응답 → 스테이션 Recipes/{productId}.json 모양 (recipeId·active·createdAt은 메타로 뺀다).</summary>
    public static StationRecipeFile ToStationRecipe(MesRecipeDetail d) =>
        new(d.ProductId, d.Version, d.Note,
            (d.Parts ?? new()).Select(p => new StationRecipePart(p.PartId, p.ExpectedClass, p.Holes ?? new())).ToList());

    /// <returns>문제가 없으면 null, 있으면 사람이 읽는 이유 한 줄</returns>
    public static string? Validate(StationRecipeFile r, string expectedProductId)
    {
        if (r.ProductId != expectedProductId) return $"제품 ID가 목록과 다름: {r.ProductId}";
        if (r.Version <= 0) return $"버전이 잘못됨: {r.Version}";
        if (r.Parts.Count == 0) return "부품이 없음";

        var ids = new HashSet<string>();
        foreach (var p in r.Parts)
        {
            if (string.IsNullOrWhiteSpace(p.PartId) || !ids.Add(p.PartId)) return $"부품 ID가 비었거나 중복: {p.PartId}";
            if (!ValidClasses.Contains(p.ExpectedClass)) return $"{p.PartId}: 모르는 클래스 이름 {p.ExpectedClass}";
            if (p.Holes.Count == 0) return $"{p.PartId}: 홀이 없음";
            foreach (var h in p.Holes)
            {
                if (!HolePattern.IsMatch(h) || Hole.Parse(h) is not { } hole || !hole.IsValid())
                    return $"{p.PartId}: 홀 형식이 잘못됨 {h}";
            }
        }
        return null;
    }

    private string RecipePath(string productId) => Path.Combine(_folder.Path, $"{productId}.json");

    /// <summary>기존 파일 백업 → 임시 파일에 다 쓴 뒤 이름 바꾸기 (쓰는 도중 꺼져도 반쪽짜리 레시피가 남지 않게).</summary>
    private void Save(string productId, StationRecipeFile file)
    {
        Directory.CreateDirectory(_folder.Path);
        string path = RecipePath(productId);

        if (File.Exists(path))
        {
            string backupDir = Path.Combine(_folder.Path, "backup");
            Directory.CreateDirectory(backupDir);
            string backup = Path.Combine(backupDir, $"{productId}.v{ReadVersion(path)}.json");
            if (!File.Exists(backup)) File.Copy(path, backup);   // 같은 버전 백업이 이미 있으면 처음 것을 둔다
        }

        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(file, WriteJson));
        File.Move(temp, path, overwrite: true);
    }

    private static string ReadVersion(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("version", out var v) ? v.ToString() : "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }
}

// MES 응답 (GET /api/recipes, GET /api/recipes/{id}) — tests/Fixtures에 원문이 있다
public record MesRecipeSummary(Guid RecipeId, string ProductId, int Version, string? Note, bool Active, DateTimeOffset CreatedAt);
public record MesRecipePart(string PartId, string ExpectedClass, List<string>? Holes);
public record MesRecipeDetail(Guid RecipeId, string ProductId, int Version, string? Note, bool Active,
    DateTimeOffset CreatedAt, List<MesRecipePart>? Parts);

// 스테이션 레시피 파일 (Recipes/{productId}.json, FileRecipeProvider가 읽는 모양)
public record StationRecipeFile(string ProductId, int Version, string? Note, List<StationRecipePart> Parts);
public record StationRecipePart(string PartId, string ExpectedClass, List<string> Holes);
