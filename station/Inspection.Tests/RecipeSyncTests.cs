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
/// MES 응답은 LG MES에서 받은 실제 원문(Fixtures/)을 가짜 HTTP 처리기로 돌려준다. 네트워크·MES 없이 돈다.
/// </summary>
public class RecipeSyncTests : IDisposable
{
    private static readonly Guid RecipeB = Guid.Parse("7bafaa9d-4d9f-4d2e-bc04-0db47d401003");
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recipe-sync-tests", Guid.NewGuid().ToString("N"));

    public RecipeSyncTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private string RecipePath(string productId) => Path.Combine(_dir, $"{productId}.json");

    /// <summary>경로별로 정해 둔 응답을 돌려주고, 받은 요청을 기록하는 가짜 MES.</summary>
    private sealed class FakeMes : HttpMessageHandler
    {
        public readonly Dictionary<string, (HttpStatusCode Status, string Body)> Routes = new();
        public readonly List<string> Requests = new();
        public string? LastAuthorization;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            LastAuthorization = request.Headers.Authorization?.ToString();
            var (status, body) = Routes.TryGetValue(path, out var r) ? r : (HttpStatusCode.NotFound, "{}");
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        public int DetailRequests => Requests.Count(p => p.StartsWith("/api/recipes/"));
    }

    private static FakeMes MesWithFixtures(string? detailOverride = null)
    {
        var mes = new FakeMes();
        mes.Routes["/api/recipes"] = (HttpStatusCode.OK, Fixture("mes-recipes-list.json"));
        mes.Routes[$"/api/recipes/{RecipeB}"] = (HttpStatusCode.OK, detailOverride ?? Fixture("mes-recipe-NE555-B.json"));
        return mes;
    }

    private (RecipeSyncService Sync, RecipeMetaStore Meta, FileRecipeProvider Recipes) Create(FakeMes mes)
    {
        var folder = new RecipeFolder(_dir);
        var meta = new RecipeMetaStore(folder);
        var recipes = new FileRecipeProvider(folder);
        var sync = new RecipeSyncService(
            () => new HttpClient(mes, disposeHandler: false) { BaseAddress = new Uri("http://mes.test") },
            Options.Create(new MesOptions()), folder, meta, recipes, NullLogger<RecipeSyncService>.Instance);
        return (sync, meta, recipes);
    }

    /// <summary>스테이션 형식의 기존(로컬) 레시피 파일. 동기화가 실패하면 이 내용이 그대로 남아야 한다.</summary>
    private void WriteLocalRecipe(string productId, int version) =>
        File.WriteAllText(RecipePath(productId), $$"""
            {
              "productId": "{{productId}}",
              "version": {{version}},
              "note": "local",
              "parts": [ { "partId": "R1", "expectedClass": "resistance470", "holes": [ "E9", "H9" ] } ]
            }
            """);

    [Fact]
    public async Task MES_응답을_스테이션_레시피로_바꿔_저장하고_캐시를_버린다()
    {
        WriteLocalRecipe("NE555-B", 2);
        var mes = MesWithFixtures();
        var (sync, meta, recipes) = Create(mes);
        Assert.Equal(2, (await recipes.GetAsync("NE555-B")).Version);   // 캐시에 옛 레시피가 올라가 있는 상태

        var result = await sync.SyncAsync("test-token", CancellationToken.None);

        Assert.Equal(new[] { "NE555-B" }, result.Updated);
        Assert.Empty(result.Failed);
        Assert.Equal("Bearer test-token", mes.LastAuthorization);

        // 스테이션이 실제로 읽는 경로(FileRecipeProvider)로 확인 → 캐시가 무효화돼 새 파일을 읽는다
        var recipe = await recipes.GetAsync("NE555-B");
        Assert.Equal(3, recipe.Version);
        Assert.Equal(12, recipe.Parts.Count);
        var r1 = recipe.Parts.Single(p => p.PartId == "R1");
        Assert.Equal("resistance330", r1.ExpectedClass);
        Assert.Equal(new[] { new Hole('E', 9), new Hole('H', 9) }, r1.Holes);
        Assert.Equal(14, recipe.Parts.Single(p => p.PartId == "U1").Holes.Count);

        // 기존 파일은 이전 버전 이름으로 백업, 메타에 출처 기록
        Assert.True(File.Exists(Path.Combine(_dir, "backup", "NE555-B.v2.json")));
        Assert.Equal(RecipeB, meta.Get("NE555-B")!.RecipeId);
        Assert.Equal(3, meta.Get("NE555-B")!.Version);
        Assert.True(File.Exists(Path.Combine(_dir, RecipeMetaStore.FileName)));
        Assert.False(File.Exists(RecipePath("NE555-B") + ".tmp"));
    }

    [Fact]
    public async Task 같은_recipeId면_상세를_다시_받지_않고_파일도_그대로_둔다()
    {
        var mes = MesWithFixtures();
        var (sync, _, _) = Create(mes);
        await sync.SyncAsync("t", CancellationToken.None);
        byte[] before = File.ReadAllBytes(RecipePath("NE555-B"));
        var writtenAt = File.GetLastWriteTimeUtc(RecipePath("NE555-B"));

        var second = await sync.SyncAsync("t", CancellationToken.None);

        Assert.Equal(new[] { "NE555-B" }, second.Skipped);
        Assert.Empty(second.Updated);
        Assert.Equal(1, mes.DetailRequests);   // 첫 번째 동기화에서만 상세 조회
        Assert.Equal(before, File.ReadAllBytes(RecipePath("NE555-B")));
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(RecipePath("NE555-B")));
    }

    [Theory]
    [InlineData("list")]     // 목록 조회 실패 (MES 오류)
    [InlineData("detail")]   // 상세 조회 실패
    public async Task 조회에_실패하면_기존_파일과_메타를_그대로_둔다(string failAt)
    {
        WriteLocalRecipe("NE555-B", 2);
        byte[] before = File.ReadAllBytes(RecipePath("NE555-B"));
        var mes = MesWithFixtures();
        string failing = failAt == "list" ? "/api/recipes" : $"/api/recipes/{RecipeB}";
        mes.Routes[failing] = (HttpStatusCode.InternalServerError, "{\"status\":500}");
        var (sync, meta, recipes) = Create(mes);

        var result = await sync.SyncAsync("t", CancellationToken.None);

        Assert.Empty(result.Updated);
        Assert.True(result.Error is not null || result.Failed.ContainsKey("NE555-B"));
        Assert.Equal(before, File.ReadAllBytes(RecipePath("NE555-B")));
        Assert.Null(meta.Get("NE555-B"));
        Assert.False(Directory.Exists(Path.Combine(_dir, "backup")));
        Assert.Equal(2, (await recipes.GetAsync("NE555-B")).Version);   // 검사는 로컬 레시피로 계속
    }

    [Theory]
    [InlineData("\"Q10\",\"R10\"", "\"Q10\",\"Z99\"")]                                        // 없는 홀
    [InlineData("\"H0\",\"H2\"", "\"E0\",\"H2\"")]                                            // 전원 레일에 없는 E행
    [InlineData("\"expectedClass\":\"capacitor\"", "\"expectedClass\":\"capacitor_x\"")]    // 모르는 클래스
    [InlineData("\"parts\":[{", "\"parts\":[],\"x\":[{")]                                    // 부품 0개
    public async Task 검증에_실패하면_기존_파일을_그대로_둔다(string find, string replace)
    {
        WriteLocalRecipe("NE555-B", 2);
        byte[] before = File.ReadAllBytes(RecipePath("NE555-B"));
        string broken = Fixture("mes-recipe-NE555-B.json").Replace(find, replace);
        Assert.NotEqual(Fixture("mes-recipe-NE555-B.json"), broken);
        var (sync, meta, _) = Create(MesWithFixtures(broken));

        var result = await sync.SyncAsync("t", CancellationToken.None);

        Assert.True(result.Failed.ContainsKey("NE555-B"));
        Assert.Empty(result.Updated);
        Assert.Equal(before, File.ReadAllBytes(RecipePath("NE555-B")));
        Assert.Null(meta.Get("NE555-B"));
        Assert.False(Directory.Exists(Path.Combine(_dir, "backup")));
    }

    [Fact]
    public void 판정불가_기록의_errorCode와_recipeId가_MES_본문에_그대로_실린다()
    {
        var invalid = new InspectionResult(Guid.NewGuid(), Verdict.Invalid, 0, new List<PartResult>(),
            null, 1234, "BOARD_NOT_RECOGNIZED");

        var record = MesInspectionRecord.From(invalid, "ST-01", "NE555-B", "A-1042",
            DateTimeOffset.Parse("2026-09-28T18:04:32+09:00"), RecipeB);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = json.RootElement;

        Assert.Equal("INVALID", root.GetProperty("verdict").GetString());
        Assert.Equal("BOARD_NOT_RECOGNIZED", root.GetProperty("errorCode").GetString());
        Assert.Equal(RecipeB, root.GetProperty("recipeId").GetGuid());
        Assert.Equal(0, root.GetProperty("parts").GetArrayLength());

        // 메타가 없으면 recipeId는 null (MES가 활성 레시피로 대체)
        var noMeta = MesInspectionRecord.From(invalid, "ST-01", "NE555-A", null, DateTimeOffset.Now);
        Assert.Null(noMeta.RecipeId);
    }
}
