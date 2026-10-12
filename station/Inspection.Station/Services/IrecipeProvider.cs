using Inspection.Contracts;

namespace Inspection.Station.Services;

/// <summary>레시피(정답지)를 가져온다. 지금은 JSON 파일, 나중에 MES(Java)로 교체.</summary>
public interface IRecipeProvider
{
    Task<Recipe> GetAsync(string productId, CancellationToken ct = default);

    /// <summary>캐시를 버려 다음 검사부터 파일을 다시 읽게 한다 (MES 레시피 동기화 뒤).</summary>
    void Invalidate(string productId);
}

/// <summary>
/// 원본 레시피 JSON 파일이 있는 폴더 (실행 폴더 기준 Recipes:Folder). 스테이션은 여기를 읽기만 한다.
/// MES에서 받은 레시피는 따로 recipes-cache/에 둔다 (RecipeCache).
/// </summary>
public sealed class RecipeFolder
{
    public RecipeFolder(string path) => Path = path;

    public string Path { get; }

    /// <summary>원본 레시피가 있는 제품들 (파일 이름 = 제품 ID). 로그인 때 이 제품들만 MES와 맞춰 본다.</summary>
    public IReadOnlyList<string> ProductIds() =>
        Directory.Exists(Path)
            ? Directory.GetFiles(Path, "*.json").Select(f => System.IO.Path.GetFileNameWithoutExtension(f)!)
                .OrderBy(p => p, StringComparer.Ordinal).ToList()
            : [];

    public static RecipeFolder FromConfig(IConfiguration config) =>
        new(System.IO.Path.Combine(AppContext.BaseDirectory, config["Recipes:Folder"] ?? "Recipes"));
}
