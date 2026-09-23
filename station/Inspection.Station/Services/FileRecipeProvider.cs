using System.Text.Json;
using Inspection.Contracts;

namespace Inspection.Station.Services;

/// <summary>
/// JSON 파일에서 레시피를 읽는다. MES(Java)가 준비되기 전까지 쓰는 구현.
/// 한 번 읽으면 메모리에 캐시한다 (검사할 때마다 파일을 읽지 않는다).
/// </summary>
public class FileRecipeProvider : IRecipeProvider
{
    private readonly string _folder;
    private readonly Dictionary<string, Recipe> _cache = new();

    public FileRecipeProvider(IConfiguration config)
    {
        _folder = config["Recipes:Folder"] ?? "Recipes";
    }

    public async Task<Recipe> GetAsync(string productId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(productId, out var cached)) return cached;

        var path = Path.Combine(AppContext.BaseDirectory, _folder, $"{productId}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"레시피 파일이 없습니다: {path}");

        var json = await File.ReadAllTextAsync(path, ct);
        var dto = JsonSerializer.Deserialize<RecipeDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("레시피를 읽을 수 없습니다.");

        var recipe = new Recipe(dto.ProductId, dto.Version,
            dto.Parts.Select(p => new RecipePart(
                p.PartId,
                p.ExpectedClass,
                p.Holes.Select(h => Hole.Parse(h)
                    ?? throw new InvalidOperationException($"홀 라벨이 잘못됐습니다: {h}"))
                    .ToList()
            )).ToList());

        _cache[productId] = recipe;
        return recipe;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // JSON 파일 구조 (홀이 "A0" 문자열 배열)
    private record RecipeDto(string ProductId, int Version, List<PartDto> Parts);
    private record PartDto(string PartId, string ExpectedClass, List<string> Holes);
}