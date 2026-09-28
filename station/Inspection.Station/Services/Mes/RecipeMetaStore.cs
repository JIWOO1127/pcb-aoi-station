using System.Text.Encodings.Web;
using System.Text.Json;

namespace Inspection.Station.Services.Mes;

/// <summary>MES에서 받아 온 레시피 한 건의 출처. 검사 기록에 recipeId를 붙일 때 쓴다.</summary>
public record RecipeMeta(Guid RecipeId, int Version, DateTimeOffset SyncedAt);

/// <summary>
/// Recipes/recipes-meta.json — 제품별로 "지금 파일이 MES의 어느 레시피에서 왔는지".
/// MES에서 받은 적 없는 제품(로컬에서만 만든 레시피)은 여기에 없다 → 검사 기록의 recipeId는 null.
/// </summary>
public sealed class RecipeMetaStore
{
    public const string FileName = "recipes-meta.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, RecipeMeta> _items;

    public RecipeMetaStore(RecipeFolder folder)
    {
        _path = Path.Combine(folder.Path, FileName);
        _items = Load(_path);
    }

    public RecipeMeta? Get(string productId)
    {
        lock (_lock) return _items.TryGetValue(productId, out var m) ? m : null;
    }

    public IReadOnlyDictionary<string, RecipeMeta> Snapshot()
    {
        lock (_lock) return new Dictionary<string, RecipeMeta>(_items);
    }

    /// <summary>한 제품의 출처를 바꾸고 파일에 저장한다 (임시 파일 → 이름 바꾸기).</summary>
    public void Set(string productId, RecipeMeta meta)
    {
        lock (_lock)
        {
            var next = new Dictionary<string, RecipeMeta>(_items) { [productId] = meta };
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next, Json));
            File.Move(temp, _path, overwrite: true);
            _items = next;
        }
    }

    private static Dictionary<string, RecipeMeta> Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, RecipeMeta>>(File.ReadAllText(path), Json) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();   // 깨진 메타는 무시한다 (다음 동기화에서 다시 쓴다). 레시피 파일 자체는 그대로.
        }
    }
}
