using System.Collections.Concurrent;
using System.Text.Json;
using Inspection.Contracts;

namespace Inspection.Station.Services;

/// <summary>
/// JSON 파일에서 레시피를 읽는다.
/// 한 번 읽으면 메모리에 캐시한다 (검사할 때마다 파일을 읽지 않는다).
/// MES 레시피 동기화가 파일을 바꾸면 Invalidate로 캐시를 버린다.
/// </summary>
public class FileRecipeProvider : IRecipeProvider
{
    private readonly RecipeFolder _folder;
    // 로그인(동기화)과 검사가 동시에 들어올 수 있어 동시성 안전한 사전을 쓴다
    private readonly ConcurrentDictionary<string, Recipe> _cache = new();

    public FileRecipeProvider(RecipeFolder folder)
    {
        _folder = folder;
    }

    public async Task<Recipe> GetAsync(string productId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(productId, out var cached)) return cached;

        var path = Path.Combine(_folder.Path, $"{productId}.json");
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

    // JSON 파일 구조 (홀이 "A0" 문자열 배열)
    private record RecipeDto(string ProductId, int Version, List<PartDto> Parts);
    private record PartDto(string PartId, string ExpectedClass, List<string> Holes);
}
