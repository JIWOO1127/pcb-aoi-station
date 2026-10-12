using Inspection.Station.Services.Mes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Controllers;

/// <summary>
/// GET /api/recipes/status — MES에서 받아 둔 레시피(recipes-cache)와 마지막 동기화 결과. 확인용.
///   { syncEnabled, lastSync: { at, items:[{ productId, status, version, recipeId, approvedBy, approvedAt }] } | null,
///     cache: { productId: { productId, version, recipeId, approvedBy, approvedAt, syncedAt, ... } } }
/// </summary>
[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly RecipeCache _cache;
    private readonly RecipeSyncService _sync;
    private readonly MesOptions _options;

    public RecipesController(RecipeCache cache, RecipeSyncService sync, IOptions<MesOptions> options)
    {
        _cache = cache;
        _sync = sync;
        _options = options.Value;
    }

    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        syncEnabled = _options.RecipeSyncEnabled,
        lastSync = _sync.LastResult,
        cache = _cache.Snapshot(),
    });
}
