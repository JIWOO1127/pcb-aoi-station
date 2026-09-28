using Inspection.Contracts;

namespace Inspection.Station.Services;

/// <summary>레시피(정답지)를 가져온다. 지금은 JSON 파일, 나중에 MES(Java)로 교체.</summary>
public interface IRecipeProvider
{
    Task<Recipe> GetAsync(string productId, CancellationToken ct = default);

    /// <summary>캐시를 버려 다음 검사부터 파일을 다시 읽게 한다 (MES 레시피 동기화 뒤).</summary>
    void Invalidate(string productId);
}

/// <summary>레시피 JSON 파일이 있는 폴더 (실행 폴더 기준 Recipes:Folder). 읽기·동기화가 같은 곳을 쓰게 한 곳에서 정한다.</summary>
public sealed class RecipeFolder
{
    public RecipeFolder(string path) => Path = path;

    public string Path { get; }

    public static RecipeFolder FromConfig(IConfiguration config) =>
        new(System.IO.Path.Combine(AppContext.BaseDirectory, config["Recipes:Folder"] ?? "Recipes"));
}
