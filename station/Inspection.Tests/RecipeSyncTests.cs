using System.Net;
using System.Text;
using System.Text.Json;
using Inspection.Contracts;
using Inspection.Station.Services;
using Inspection.Station.Services.Mes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Inspection.Tests;

/// <summary>
/// 로그인 때 MES 레시피 동기화 (RecipeSyncService) 테스트.
/// MES 응답은 실제 MES에서 받은 원문(Fixtures/)을 가짜 HTTP 처리기로 돌려준다. 네트워크·MES 없이 돈다.
/// </summary>
public class RecipeSyncTests : IDisposable
{
    private static readonly Guid RecipeB = Guid.Parse("7bafaa9d-4d9f-4d2e-bc04-0db47d401003");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "recipe-sync-tests", Guid.NewGuid().ToString("N"));
    private string RecipesDir => Path.Combine(_root, "Recipes");
    private string CacheDir => Path.Combine(_root, "recipes-cache");

    public RecipeSyncTests() => Directory.CreateDirectory(RecipesDir);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private string OriginalPath(string productId) => Path.Combine(RecipesDir, $"{productId}.json");
    private string CachePath(string productId) => Path.Combine(CacheDir, $"{productId}.json");

    /// <summary>경로별로 정해 둔 응답을 돌려주고, 받은 요청을 기록하는 가짜 MES.</summary>
    private sealed class FakeMes : HttpMessageHandler
    {
        public readonly Dictionary<string, (HttpStatusCode Status, string Body)> Routes = new();
        public readonly List<string> Requests = new();
        public string? LastAuthorization;
        public TimeSpan Delay = TimeSpan.Zero;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            LastAuthorization = request.Headers.Authorization?.ToString();
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            var (status, body) = Routes.TryGetValue(path, out var r) ? r : (HttpStatusCode.NotFound, "{}");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        public int DetailRequests => Requests.Count(p => p.StartsWith("/api/recipes/"));
    }

    /// <summary>MES 원문을 바탕으로 NE555-B 활성 레시피의 버전·승인 정보를 바꾼 가짜 MES (목록·상세 둘 다).</summary>
    private static FakeMes Mes(int version = 3, string? approvedBy = null, string? approvedAt = null, string? detailBody = null)
    {
        var list = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(Fixture("mes-recipes-list.json"))!;
        foreach (var r in list.Where(r => r["productId"]?.ToString() == "NE555-B")) r["version"] = version;

        var detail = JsonSerializer.Deserialize<Dictionary<string, object?>>(Fixture("mes-recipe-NE555-B.json"))!;
        detail["version"] = version;
        detail["approvedBy"] = approvedBy;
        detail["approvedAt"] = approvedAt;

        var mes = new FakeMes();
        mes.Routes["/api/recipes"] = (HttpStatusCode.OK, JsonSerializer.Serialize(list));
        mes.Routes[$"/api/recipes/{RecipeB}"] = (HttpStatusCode.OK, detailBody ?? JsonSerializer.Serialize(detail));
        return mes;
    }

    private (RecipeSyncService Sync, RecipeCache Cache, FileRecipeProvider Recipes) Create(FakeMes mes)
    {
        var folder = new RecipeFolder(RecipesDir);
        var cache = new RecipeCache(CacheDir);
        var recipes = new FileRecipeProvider(folder, cache);
        var sync = new RecipeSyncService(
            () => new HttpClient(mes, disposeHandler: false) { BaseAddress = new Uri("http://mes.test") },
            Options.Create(new MesOptions()), folder, cache, recipes, NullLogger<RecipeSyncService>.Instance);
        return (sync, cache, recipes);
    }

    /// <summary>원본 레시피 파일 (스테이션 Recipes/). 동기화가 무엇을 하든 이 파일은 그대로여야 한다.</summary>
    private void WriteOriginal(string productId, int version) =>
        File.WriteAllText(OriginalPath(productId), $$"""
            {
              "productId": "{{productId}}",
              "version": {{version}},
              "note": "local",
              "parts": [ { "partId": "R1", "expectedClass": "resistance470", "holes": [ "E9", "H9" ] } ]
            }
            """);

    private static RecipeSyncItem Item(RecipeSyncResult r, string productId) => r.Items.Single(i => i.ProductId == productId);

    [Fact]
    public async Task A_MES와_같은_버전이면_UNCHANGED_캐시를_만들지_않는다()
    {
        WriteOriginal("NE555-B", 3);
        byte[] original = File.ReadAllBytes(OriginalPath("NE555-B"));
        var mes = Mes(version: 3);
        var (sync, _, recipes) = Create(mes);

        var result = await sync.SyncAsync("t", CancellationToken.None);

        var b = Item(result, "NE555-B");
        Assert.Equal(RecipeSyncStatus.Unchanged, b.Status);
        Assert.Equal(3, b.Version);
        Assert.Null(b.RecipeId);                               // 원본을 쓰는 중 → recipeId 없음
        Assert.Equal(0, mes.DetailRequests);                   // 같은 버전이면 상세를 받지 않는다
        Assert.False(File.Exists(CachePath("NE555-B")));
        Assert.Equal(original, File.ReadAllBytes(OriginalPath("NE555-B")));
        Assert.Equal(3, (await recipes.GetAsync("NE555-B")).Version);
    }

    [Fact]
    public async Task B_MES_버전이_높으면_UPDATED_캐시에_저장하고_원본은_그대로_판정은_캐시로()
    {
        WriteOriginal("NE555-B", 3);
        byte[] original = File.ReadAllBytes(OriginalPath("NE555-B"));
        var mes = Mes(version: 4, approvedBy: "E-0002", approvedAt: "2026-09-28T09:10:00Z");
        var (sync, cache, recipes) = Create(mes);
        Assert.Equal(3, (await recipes.GetAsync("NE555-B")).Version);   // 메모리 캐시에 옛 레시피가 올라간 상태

        var result = await sync.SyncAsync("test-token", CancellationToken.None);

        var b = Item(result, "NE555-B");
        Assert.Equal(RecipeSyncStatus.Updated, b.Status);
        Assert.Equal(4, b.Version);
        Assert.Equal(RecipeB, b.RecipeId);
        Assert.Equal("E-0002", b.ApprovedBy);
        Assert.Equal(DateTimeOffset.Parse("2026-09-28T09:10:00Z"), b.ApprovedAt);
        Assert.Equal("Bearer test-token", mes.LastAuthorization);

        // 캐시 파일에 출처가 함께 저장되고, 원본은 한 바이트도 바뀌지 않는다
        var cached = cache.Get("NE555-B")!;
        Assert.Equal((4, RecipeB, "E-0002"), (cached.Version, cached.RecipeId, cached.ApprovedBy));
        Assert.Equal(original, File.ReadAllBytes(OriginalPath("NE555-B")));
        Assert.False(File.Exists(CachePath("NE555-B") + ".tmp"));

        // 판정은 캐시 레시피로 (메모리 캐시 무효화 확인)
        var recipe = await recipes.GetAsync("NE555-B");
        Assert.Equal(4, recipe.Version);
        Assert.Equal(12, recipe.Parts.Count);
        Assert.Equal("resistance330", recipe.Parts.Single(p => p.PartId == "R1").ExpectedClass);
        Assert.Equal(new[] { new Hole('E', 9), new Hole('H', 9) }, recipe.Parts.Single(p => p.PartId == "R1").Holes);
        Assert.Equal(14, recipe.Parts.Single(p => p.PartId == "U1").Holes.Count);
    }

    [Fact]
    public async Task 캐시가_이미_같은_버전이면_다시_받지_않고_UNCHANGED에_캐시의_recipeId()
    {
        WriteOriginal("NE555-B", 3);
        var mes = Mes(version: 4, approvedBy: "E-0002");
        var (sync, _, _) = Create(mes);
        await sync.SyncAsync("t", CancellationToken.None);
        var writtenAt = File.GetLastWriteTimeUtc(CachePath("NE555-B"));

        var second = await sync.SyncAsync("t", CancellationToken.None);

        var b = Item(second, "NE555-B");
        Assert.Equal(RecipeSyncStatus.Unchanged, b.Status);
        Assert.Equal((4, (Guid?)RecipeB, "E-0002"), (b.Version, b.RecipeId, b.ApprovedBy));
        Assert.Equal(1, mes.DetailRequests);   // 첫 번째 동기화에서만 상세 조회
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(CachePath("NE555-B")));
    }

    [Fact]
    public async Task MES_버전이_더_낮으면_받지_않는다()
    {
        WriteOriginal("NE555-B", 5);
        var mes = Mes(version: 4);
        var (sync, _, _) = Create(mes);

        var b = Item(await sync.SyncAsync("t", CancellationToken.None), "NE555-B");

        Assert.Equal((RecipeSyncStatus.Unchanged, (int?)5), (b.Status, b.Version));
        Assert.Equal(0, mes.DetailRequests);
        Assert.False(File.Exists(CachePath("NE555-B")));
    }

    [Fact]
    public async Task MES에_활성_레시피가_없는_제품은_NOT_FOUND_원본_유지()
    {
        WriteOriginal("NE555-A", 4);
        WriteOriginal("NE555-B", 3);
        var (sync, _, recipes) = Create(Mes(version: 3));

        var result = await sync.SyncAsync("t", CancellationToken.None);

        Assert.Equal(new[] { "NE555-A", "NE555-B" }, result.Items.Select(i => i.ProductId));   // 원본이 있는 제품만, MES의 PCB-* 제외
        Assert.Equal((RecipeSyncStatus.NotFound, (int?)4), (Item(result, "NE555-A").Status, Item(result, "NE555-A").Version));
        Assert.Equal(4, (await recipes.GetAsync("NE555-A")).Version);
    }

    [Theory]
    [InlineData("list", false)]     // 목록 조회 실패 (MES 오류)
    [InlineData("detail", false)]   // 상세 조회 실패
    [InlineData("list", true)]      // 시간 초과
    public async Task C_MES_실패나_시간_초과면_OFFLINE_지금_레시피_유지(string failAt, bool timeout)
    {
        WriteOriginal("NE555-B", 3);
        byte[] original = File.ReadAllBytes(OriginalPath("NE555-B"));
        var mes = Mes(version: 4);
        if (timeout) mes.Delay = TimeSpan.FromSeconds(10);
        else mes.Routes[failAt == "list" ? "/api/recipes" : $"/api/recipes/{RecipeB}"] =
            (HttpStatusCode.InternalServerError, "{\"status\":500}");
        var (sync, _, recipes) = Create(mes);
        using var limit = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var result = await sync.SyncAsync("t", timeout ? limit.Token : CancellationToken.None);

        var b = Item(result, "NE555-B");
        Assert.Equal((RecipeSyncStatus.Offline, (int?)3), (b.Status, b.Version));
        Assert.False(File.Exists(CachePath("NE555-B")));
        Assert.Equal(original, File.ReadAllBytes(OriginalPath("NE555-B")));
        Assert.Equal(3, (await recipes.GetAsync("NE555-B")).Version);
    }

    [Fact]
    public async Task 오프라인_로그인은_모든_제품_OFFLINE_마지막으로_받은_버전()
    {
        WriteOriginal("NE555-A", 4);
        WriteOriginal("NE555-B", 3);
        var (sync, _, _) = Create(Mes(version: 4, approvedBy: "E-0002"));
        await sync.SyncAsync("t", CancellationToken.None);   // NE555-B v4를 받아 둔 상태

        var items = await sync.OfflineAsync(CancellationToken.None);

        Assert.All(items, i => Assert.Equal(RecipeSyncStatus.Offline, i.Status));
        Assert.Equal((4, (Guid?)RecipeB), (items.Single(i => i.ProductId == "NE555-B").Version, items.Single(i => i.ProductId == "NE555-B").RecipeId));
        Assert.Equal((4, (Guid?)null), (items.Single(i => i.ProductId == "NE555-A").Version, items.Single(i => i.ProductId == "NE555-A").RecipeId));
    }

    [Theory]
    [InlineData("\"Q10\",\"R10\"", "\"Q10\",\"Z99\"")]                                        // 없는 홀
    [InlineData("\"H0\",\"H2\"", "\"E0\",\"H2\"")]                                            // 전원 레일에 없는 E행
    [InlineData("\"expectedClass\":\"capacitor\"", "\"expectedClass\":\"capacitor_x\"")]    // 모르는 클래스
    [InlineData("\"parts\":[{", "\"parts\":[],\"x\":[{")]                                    // 부품 0개
    [InlineData("{", "")]                                                                     // JSON이 깨짐
    public async Task 받은_레시피가_깨졌으면_INVALID_저장하지_않고_유지(string find, string replace)
    {
        WriteOriginal("NE555-B", 3);
        string body = Fixture("mes-recipe-NE555-B.json").Replace("\"version\":3", "\"version\":4");
        string broken = find == "{" ? body[1..] : body.Replace(find, replace);
        Assert.NotEqual(body, broken);
        var (sync, _, recipes) = Create(Mes(version: 4, detailBody: broken));

        var b = Item(await sync.SyncAsync("t", CancellationToken.None), "NE555-B");

        Assert.Equal((RecipeSyncStatus.Invalid, (int?)3), (b.Status, b.Version));
        Assert.False(Directory.Exists(CacheDir) && Directory.GetFiles(CacheDir).Length > 0);
        Assert.Equal(3, (await recipes.GetAsync("NE555-B")).Version);
    }

    [Fact]
    public async Task 캐시_파일이_깨졌으면_원본으로_판정한다()
    {
        WriteOriginal("NE555-B", 3);
        Directory.CreateDirectory(CacheDir);
        File.WriteAllText(CachePath("NE555-B"), "{ not json");
        var (_, _, recipes) = Create(Mes());

        Assert.Equal(3, (await recipes.GetAsync("NE555-B")).Version);
    }

    [Fact]
    public async Task 검사_기록의_recipeId는_캐시에_있을_때만()
    {
        WriteOriginal("NE555-A", 4);
        WriteOriginal("NE555-B", 3);
        var (sync, cache, _) = Create(Mes(version: 4));
        await sync.SyncAsync("t", CancellationToken.None);
        var invalid = new InspectionResult(Guid.NewGuid(), Verdict.Invalid, 0, new List<PartResult>(),
            null, 1234, "BOARD_NOT_RECOGNIZED");

        var record = MesInspectionRecord.From(invalid, "ST-01", "NE555-B", "E-0001",
            DateTimeOffset.Parse("2026-09-28T18:04:32+09:00"), cache.Get("NE555-B")?.RecipeId);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = json.RootElement;

        Assert.Equal("INVALID", root.GetProperty("verdict").GetString());
        Assert.Equal("BOARD_NOT_RECOGNIZED", root.GetProperty("errorCode").GetString());
        Assert.Equal(RecipeB, root.GetProperty("recipeId").GetGuid());

        // 원본 레시피를 쓰는 제품은 null (MES가 활성 레시피로 대체)
        var noCache = MesInspectionRecord.From(invalid, "ST-01", "NE555-A", null, DateTimeOffset.Now, cache.Get("NE555-A")?.RecipeId);
        Assert.Null(noCache.RecipeId);
    }
}
