using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inspection.Contracts;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Services.Mes;

/// <summary>/api/login 응답 recipeSync[].status 값.</summary>
public static class RecipeSyncStatus
{
    public const string Updated = "UPDATED";       // MES 버전이 더 높아 recipes-cache에 새로 저장 → 이번 검사부터 적용
    public const string Unchanged = "UNCHANGED";   // MES 버전이 지금 쓰는 것과 같거나 낮음
    public const string Offline = "OFFLINE";       // MES 실패·시간 초과·오프라인 로그인 → 지금 쓰는 레시피 유지
    public const string NotFound = "NOT_FOUND";    // MES에 이 제품의 활성 레시피가 없음 → 지금 쓰는 레시피 유지
    public const string Invalid = "INVALID";       // 받은 레시피가 비었거나 형식이 깨짐 → 저장하지 않고 유지
}

/// <summary>
/// 제품 하나의 동기화 결과 (/api/login 응답 recipeSync[] 한 줄).
/// version·recipeId·approvedBy·approvedAt은 "이 결과 뒤 판정에 쓰는 레시피" 기준.
/// UPDATED가 아니면 지금 쓰는 레시피(캐시가 있으면 캐시, 없으면 원본 — 원본은 recipeId·승인 정보가 null).
/// </summary>
public record RecipeSyncItem(
    string ProductId,
    string Status,
    int? Version,
    Guid? RecipeId,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt);

/// <summary>레시피 동기화 한 번의 결과. GET /api/recipes/status로도 보여 준다.</summary>
public record RecipeSyncResult(DateTimeOffset At, List<RecipeSyncItem> Items);

/// <summary>
/// 로그인 때 MES의 승인(활성) 레시피를 받아 recipes-cache/{productId}.json에 둔다.
///
///   대상: 스테이션에 원본 레시피가 있는 제품 (Recipes/*.json)
///   1. GET {RecipesPath} → 제품별 active=true 중 가장 높은 버전
///   2. 지금 쓰는 레시피(캐시 또는 원본)의 버전보다 높을 때만 GET {RecipesPath}/{recipeId}
///   3. 스테이션 형식으로 바꿔 검증 (부품 1개 이상, 부품 ID 중복 없음, 모델 클래스 이름, 실재하는 홀)
///   4. recipes-cache에 저장 (recipeId·version·approvedBy·approvedAt 포함) → 메모리 캐시 무효화
///
/// 원본 Recipes 파일은 절대 쓰지 않는다. 어느 단계에서 실패해도 그 제품은 지금 쓰는 레시피로 검사를 계속한다.
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

    private readonly Func<HttpClient> _http;
    private readonly MesOptions _options;
    private readonly RecipeFolder _folder;
    private readonly RecipeCache _cache;
    private readonly IRecipeProvider _recipes;
    private readonly ILogger<RecipeSyncService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);   // 두 사람이 동시에 로그인해도 한 번에 하나만

    public RecipeSyncService(Func<HttpClient> http, IOptions<MesOptions> options, RecipeFolder folder,
        RecipeCache cache, IRecipeProvider recipes, ILogger<RecipeSyncService> log)
    {
        _http = http;
        _options = options.Value;
        _folder = folder;
        _cache = cache;
        _recipes = recipes;
        _log = log;
    }

    public RecipeSyncResult? LastResult { get; private set; }

    /// <summary>MES에 물어보지 못했을 때(오프라인 로그인 등): 모든 제품 OFFLINE + 지금 쓰는 레시피.</summary>
    public async Task<List<RecipeSyncItem>> OfflineAsync(CancellationToken ct)
    {
        var items = new List<RecipeSyncItem>();
        foreach (var productId in _folder.ProductIds())
            items.Add(await CurrentAsync(productId, RecipeSyncStatus.Offline, ct));
        return items;
    }

    /// <param name="ct">호출한 쪽이 시간 제한(Mes:RecipeSyncTimeoutSeconds)을 건다. 넘으면 남은 제품은 OFFLINE.</param>
    public async Task<RecipeSyncResult> SyncAsync(string accessToken, CancellationToken ct)
    {
        var products = _folder.ProductIds();
        var items = new List<RecipeSyncItem>();

        if (!await _gate.WaitAsync(0, CancellationToken.None))
        {
            _log.LogInformation("다른 레시피 동기화가 진행 중이라 이번 것은 건너뜀 → 지금 쓰는 레시피 유지");
            return new RecipeSyncResult(DateTimeOffset.Now, await OfflineAsync(CancellationToken.None));
        }

        try
        {
            using var http = _http();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            List<MesRecipeSummary> list;
            try
            {
                list = await http.GetFromJsonAsync<List<MesRecipeSummary>>(_options.RecipesPath, ReadJson, ct) ?? new();
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
            {
                _log.LogWarning("MES 레시피 목록 조회 실패 → 모든 제품 지금 레시피 유지: {Message}", ex.Message);
                return LastResult = new RecipeSyncResult(DateTimeOffset.Now, await OfflineAsync(CancellationToken.None));
            }

            foreach (var productId in products)
                items.Add(await SyncOneAsync(http, productId, list, ct));

            return LastResult = new RecipeSyncResult(DateTimeOffset.Now, items);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RecipeSyncItem> SyncOneAsync(HttpClient http, string productId,
        List<MesRecipeSummary> list, CancellationToken ct)
    {
        var active = list.Where(r => r.Active && r.ProductId == productId)
                         .OrderByDescending(r => r.Version)
                         .FirstOrDefault();
        if (active is null)
            return await CurrentAsync(productId, RecipeSyncStatus.NotFound, CancellationToken.None);

        int? localVersion = await CurrentVersionAsync(productId);
        if (localVersion is { } v && active.Version <= v)
            return await CurrentAsync(productId, RecipeSyncStatus.Unchanged, CancellationToken.None);

        MesRecipeDetail? detail;
        try
        {
            detail = await http.GetFromJsonAsync<MesRecipeDetail>($"{_options.RecipesPath}/{active.RecipeId}", ReadJson, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _log.LogWarning("MES 레시피 {Product} v{Version} 조회 실패 → 지금 레시피 유지: {Message}",
                productId, active.Version, ex.Message);
            return await CurrentAsync(productId, RecipeSyncStatus.Offline, CancellationToken.None);
        }
        catch (JsonException ex)
        {
            _log.LogWarning("MES 레시피 {Product} v{Version} 응답을 읽을 수 없음 → 저장하지 않고 유지: {Message}",
                productId, active.Version, ex.Message);
            return await CurrentAsync(productId, RecipeSyncStatus.Invalid, CancellationToken.None);
        }

        string? problem = detail is null ? "레시피 상세가 비어 있음" : Validate(detail, productId);
        if (problem is not null)
        {
            _log.LogWarning("MES 레시피 {Product} v{Version} 형식 문제 → 저장하지 않고 유지: {Problem}",
                productId, active.Version, problem);
            return await CurrentAsync(productId, RecipeSyncStatus.Invalid, CancellationToken.None);
        }

        var cached = ToCachedRecipe(detail!, DateTimeOffset.Now);
        try
        {
            _cache.Save(cached);
        }
        catch (IOException ex)
        {
            _log.LogWarning("레시피 캐시 {Product} 저장 실패 → 지금 레시피 유지: {Message}", productId, ex.Message);
            return await CurrentAsync(productId, RecipeSyncStatus.Offline, CancellationToken.None);
        }

        _recipes.Invalidate(productId);
        _log.LogInformation("MES 레시피 적용: {Product} v{Old} → v{Version} ({RecipeId}, 승인 {ApprovedBy})",
            productId, localVersion, cached.Version, cached.RecipeId, cached.ApprovedBy);
        return new RecipeSyncItem(productId, RecipeSyncStatus.Updated, cached.Version, cached.RecipeId,
            cached.ApprovedBy, cached.ApprovedAt);
    }

    /// <summary>지금 판정에 쓰는 레시피의 버전 (캐시 또는 원본). 읽을 수 없으면 null → MES 것을 받는다.</summary>
    private async Task<int?> CurrentVersionAsync(string productId)
    {
        try
        {
            return (await _recipes.GetAsync(productId)).Version;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            return null;
        }
    }

    private async Task<RecipeSyncItem> CurrentAsync(string productId, string status, CancellationToken ct)
    {
        var cached = _cache.Get(productId);
        int? version = cached?.Version ?? await CurrentVersionAsync(productId);
        return new RecipeSyncItem(productId, status, version, cached?.RecipeId, cached?.ApprovedBy, cached?.ApprovedAt);
    }

    /// <summary>MES 상세 응답 → recipes-cache 한 건. 부품은 원본 레시피 파일과 같은 모양.</summary>
    public static CachedRecipe ToCachedRecipe(MesRecipeDetail d, DateTimeOffset syncedAt) =>
        new(d.ProductId, d.Version, d.Note, d.RecipeId, d.ApprovedBy, d.ApprovedAt, syncedAt,
            (d.Parts ?? new()).Select(p => new StationRecipePart(p.PartId, p.ExpectedClass ?? "", p.Holes ?? new())).ToList());

    /// <returns>문제가 없으면 null, 있으면 사람이 읽는 이유 한 줄</returns>
    public static string? Validate(MesRecipeDetail d, string expectedProductId)
    {
        if (d.ProductId != expectedProductId) return $"제품 ID가 다름: {d.ProductId}";
        if (d.Version <= 0) return $"버전이 잘못됨: {d.Version}";
        if (d.RecipeId == Guid.Empty) return "recipeId가 없음";
        if (d.Parts is not { Count: > 0 }) return "부품이 없음";

        var ids = new HashSet<string>();
        foreach (var p in d.Parts)
        {
            if (string.IsNullOrWhiteSpace(p.PartId) || !ids.Add(p.PartId)) return $"부품 ID가 비었거나 중복: {p.PartId}";
            if (!ValidClasses.Contains(p.ExpectedClass ?? "")) return $"{p.PartId}: 모르는 클래스 이름 {p.ExpectedClass}";
            if (p.Holes is not { Count: > 0 }) return $"{p.PartId}: 홀이 없음";
            foreach (var h in p.Holes)
            {
                if (h is null || !HolePattern.IsMatch(h) || Hole.Parse(h) is not { } hole || !hole.IsValid())
                    return $"{p.PartId}: 홀 형식이 잘못됨 {h}";
            }
        }
        return null;
    }
}

// MES 응답 (GET /api/recipes, GET /api/recipes/{id}) — Inspection.Tests/Fixtures에 원문이 있다
public record MesRecipeSummary(Guid RecipeId, string ProductId, int Version, bool Active);
public record MesRecipePart(string PartId, string? ExpectedClass, List<string>? Holes);
public record MesRecipeDetail(Guid RecipeId, string ProductId, int Version, string? Note, bool Active,
    string? ApprovedBy, DateTimeOffset? ApprovedAt, List<MesRecipePart>? Parts);

// 레시피 부품 (원본 Recipes/{productId}.json, recipes-cache/{productId}.json 공통 모양)
public record StationRecipePart(string PartId, string ExpectedClass, List<string> Holes);
