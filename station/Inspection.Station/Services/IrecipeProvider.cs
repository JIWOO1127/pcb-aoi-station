using Inspection.Contracts;

namespace Inspection.Station.Services;

/// <summary>레시피(정답지)를 가져온다. 지금은 JSON 파일, 나중에 MES(Java)로 교체.</summary>
public interface IRecipeProvider
{
    Task<Recipe> GetAsync(string productId, CancellationToken ct = default);
}