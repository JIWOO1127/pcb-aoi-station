using Inspection.Station.Services.Mes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Controllers;

/// <summary>
/// GET /api/recipes/status — 지금 레시피 파일들이 MES의 어느 레시피에서 왔는지 (나중에 HMI 알림용).
///   { syncEnabled, lastSync: { at, updated, skipped, failed, error } | null, recipes: { productId: { recipeId, version, syncedAt } } }
/// </summary>
[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly RecipeMetaStore _meta;
    private readonly RecipeSyncService _sync;
    private readonly MesOptions _options;

    public RecipesController(RecipeMetaStore meta, RecipeSyncService sync, IOptions<MesOptions> options)
    {
        _meta = meta;
        _sync = sync;
        _options = options.Value;
    }

    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        syncEnabled = _options.RecipeSyncEnabled,
        lastSync = _sync.LastResult,
        recipes = _meta.Snapshot(),
    });
}
