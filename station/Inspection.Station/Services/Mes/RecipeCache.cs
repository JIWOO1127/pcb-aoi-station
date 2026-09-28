using System.Text.Encodings.Web;
using System.Text.Json;

namespace Inspection.Station.Services.Mes;

/// <summary>
/// recipes-cache/{productId}.json 한 건 — MES에서 받은 승인 레시피.
/// 부품 모양은 원본 Recipes/{productId}.json과 같고(FileRecipeProvider가 그대로 읽는다), 출처 정보가 더 붙는다.
/// </summary>
public record CachedRecipe(
    string ProductId,
    int Version,
    string? Note,
    Guid RecipeId,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset SyncedAt,
    List<StationRecipePart> Parts);

/// <summary>
/// MES에서 받은 레시피를 원본 Recipes 폴더와 따로 둔다 (실행 폴더 기준 recipes-cache/).
/// 원본 레시피 파일은 절대 덮어쓰지 않는다 → 캐시 폴더를 지우면 원본으로 돌아간다.
/// 판정은 캐시에 있으면 캐시, 없으면 원본 (FileRecipeProvider).
/// </summary>
public sealed class RecipeCache
{
    public const string DefaultFolderName = "recipes-cache";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // note의 한글을 그대로 읽히게
    };

    public RecipeCache(string folder) => Folder = folder;

    public string Folder { get; }

    public static RecipeCache FromConfig(IConfiguration config) =>
        new(Path.Combine(AppContext.BaseDirectory, config["Recipes:CacheFolder"] ?? DefaultFolderName));

    public string PathOf(string productId) => Path.Combine(Folder, $"{productId}.json");

    /// <summary>캐시에 있으면 그 레시피, 없거나 읽을 수 없으면 null (→ 원본 사용).</summary>
    public CachedRecipe? Get(string productId)
    {
        string path = PathOf(productId);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<CachedRecipe>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public IReadOnlyDictionary<string, CachedRecipe> Snapshot()
    {
        var items = new Dictionary<string, CachedRecipe>();
        if (!Directory.Exists(Folder)) return items;
        foreach (var file in Directory.GetFiles(Folder, "*.json"))
        {
            if (Get(Path.GetFileNameWithoutExtension(file)) is { } r) items[r.ProductId] = r;
        }
        return items;
    }

    /// <summary>임시 파일에 다 쓴 뒤 이름 바꾸기 (쓰는 도중 꺼져도 반쪽짜리 레시피가 남지 않게).</summary>
    public void Save(CachedRecipe recipe)
    {
        Directory.CreateDirectory(Folder);
        string path = PathOf(recipe.ProductId);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(recipe, Json));
        File.Move(temp, path, overwrite: true);
    }
}
