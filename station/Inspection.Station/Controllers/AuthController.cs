using Inspection.Contracts;
using Inspection.Station.Services.Mes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Controllers;

/// <summary>
/// POST /api/login  { employeeId, pin }
///   200 { employeeId, name, role, offline:false }
///   401 사번 또는 PIN이 틀림
///   503 MES에 연결할 수 없음 → HMI가 저장해 둔 정보로 오프라인 로그인을 시도한다
/// Mes:RecipeSyncEnabled면 로그인 성공 직후 그 작업자 토큰으로 MES 레시피를 동기화한다
/// (최대 Mes:RecipeSyncTimeoutSeconds초, 실패해도 로그인은 성공).
/// </summary>
[ApiController]
[Route("api")]
public class AuthController : ControllerBase
{
    private readonly MesAuthClient _mes;
    private readonly RecipeSyncService _recipeSync;
    private readonly MesOptions _options;
    private readonly ILogger<AuthController> _log;

    public AuthController(MesAuthClient mes, RecipeSyncService recipeSync, IOptions<MesOptions> options,
        ILogger<AuthController> log)
    {
        _mes = mes;
        _recipeSync = recipeSync;
        _options = options.Value;
        _log = log;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { message = "사번과 PIN을 입력하세요." });

        var (outcome, user, token) = await _mes.LoginAsync(request, ct);

        if (outcome == MesAuthClient.Outcome.Ok && _options.RecipeSyncEnabled && !string.IsNullOrEmpty(token))
            await SyncRecipesAsync(token, ct);

        return outcome switch
        {
            MesAuthClient.Outcome.Ok => Ok(user),
            MesAuthClient.Outcome.InvalidCredentials => Unauthorized(new { message = "사번 또는 PIN이 올바르지 않습니다." }),
            _ => StatusCode(503, new { code = "MES_UNAVAILABLE", message = "MES에 연결할 수 없습니다." })
        };
    }

    /// <summary>동기화는 정해진 시간 안에서만 한다. 어떤 실패도 로그인을 막지 않는다 (기존 레시피로 검사 계속).</summary>
    private async Task SyncRecipesAsync(string token, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RecipeSyncTimeoutSeconds)));
        try
        {
            var r = await _recipeSync.SyncAsync(token, limit.Token);
            _log.LogInformation("레시피 동기화: 적용 {Updated}, 건너뜀 {Skipped}, 실패 {Failed}{Error}",
                r.Updated.Count, r.Skipped.Count, r.Failed.Count, r.Error is null ? "" : $" ({r.Error})");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "레시피 동기화 실패 → 로그인은 계속, 기존 레시피 사용");
        }
    }
}
