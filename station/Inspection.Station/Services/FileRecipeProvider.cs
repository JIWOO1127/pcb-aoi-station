using System.Collections.Concurrent;
using System.Text.Json;
using Inspection.Contracts;
using Inspection.Station.Services.Mes;

namespace Inspection.Station.Services;

/// <summary>
/// JSON 파일에서 레시피를 읽는다: recipes-cache/{제품}.json(MES에서 받은 승인 레시피)이 있으면 그것,
/// 없으면 원본 Recipes/{제품}.json.
/// 한 번 읽으면 메모리에 캐시한다 (검사할 때마다 파일을 읽지 않는다).
/// MES 레시피 동기화가 캐시 파일을 새로 쓰면 Invalidate로 메모리 캐시를 버린다.
/// </summary>
public class FileRecipeProvider : IRecipeProvider
{
    private readonly RecipeFolder _folder;
    private readonly RecipeCache _mesCache;
    // 로그인(동기화)과 검사가 동시에 들어올 수 있어 동시성 안전한 사전을 쓴다
    private readonly ConcurrentDictionary<string, Recipe> _cache = new();

    public FileRecipeProvider(RecipeFolder folder, RecipeCache mesCache)
    {
        _folder = folder;
        _mesCache = mesCache;
    }

    public async Task<Recipe> GetAsync(string productId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(productId, out var cached)) return cached;

        // MES 캐시가 깨졌으면(RecipeCache.Get → null) 원본으로 검사한다
        var path = _mesCache.Get(productId) is not null
            ? _mesCache.PathOf(productId)
            : Path.Combine(_folder.Path, $"{productId}.json");
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

    public void Invalidate(string productId) => _cache.TryRemove(productId, out _);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // JSON 파일 구조 (홀이 "A0" 문자열 배열). 캐시 파일의 recipeId·approvedBy 등은 여기서 쓰지 않는다
    private record RecipeDto(string ProductId, int Version, List<PartDto> Parts);
    private record PartDto(string PartId, string ExpectedClass, List<string> Holes);
}
