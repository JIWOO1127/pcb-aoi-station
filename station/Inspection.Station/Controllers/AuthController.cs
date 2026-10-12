using Inspection.Contracts;
using Inspection.Station.Services.Mes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Controllers;

/// <summary>
/// /api/login 200 응답. Contracts의 LoginResult와 앞 네 필드가 같고 recipeSync가 더 붙는다
/// (Inspection.Contracts는 건드리지 않으려고 스테이션 안에 따로 둔다).
/// </summary>
public record LoginResponse(string EmployeeId, string Name, string Role, bool Offline, List<RecipeSyncItem> RecipeSync);

/// <summary>
/// POST /api/login  { employeeId, pin }
///   200 { employeeId, name, role, offline:false, recipeSync:[{ productId, status, version, recipeId, approvedBy, approvedAt }] }
///   401 사번 또는 PIN이 틀림
///   503 { code:"MES_UNAVAILABLE", message, recipeSync:[... status OFFLINE] }
///       MES에 연결할 수 없음 → HMI가 저장해 둔 정보로 오프라인 로그인을 시도한다
/// Mes:RecipeSyncEnabled면 로그인 성공 직후 그 작업자 토큰으로 MES 레시피를 동기화한다
/// (최대 Mes:RecipeSyncTimeoutSeconds초, 어떤 실패도 로그인을 막지 않는다). 꺼져 있으면 recipeSync는 빈 목록.
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
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { message = "사번과 PIN을 입력하세요." });

        var (outcome, user, token) = await _mes.LoginAsync(request, ct);

        switch (outcome)
        {
            case MesAuthClient.Outcome.Ok when user is not null:
                var sync = _options.RecipeSyncEnabled && !string.IsNullOrEmpty(token)
                    ? await SyncRecipesAsync(token, ct)
                    : new List<RecipeSyncItem>();
                return Ok(new LoginResponse(user.EmployeeId, user.Name, user.Role, user.Offline, sync));

            case MesAuthClient.Outcome.InvalidCredentials:
                return Unauthorized(new { message = "사번 또는 PIN이 올바르지 않습니다." });

            default:
                // HMI가 오프라인 로그인하면 이 목록으로 "마지막으로 받은 레시피로 검사" 배너를 띄운다
                var offline = _options.RecipeSyncEnabled
                    ? await SafeOfflineAsync()
                    : new List<RecipeSyncItem>();
                return StatusCode(503, new { code = "MES_UNAVAILABLE", message = "MES에 연결할 수 없습니다.", recipeSync = offline });
        }
    }

    /// <summary>동기화는 정해진 시간 안에서만 한다. 어떤 실패도 로그인을 막지 않는다 (지금 레시피로 검사 계속).</summary>
    private async Task<List<RecipeSyncItem>> SyncRecipesAsync(string token, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RecipeSyncTimeoutSeconds)));
        try
        {
            var r = await _recipeSync.SyncAsync(token, limit.Token);
            _log.LogInformation("레시피 동기화: {Items}",
                string.Join(", ", r.Items.Select(i => $"{i.ProductId} {i.Status} v{i.Version}")));
            return r.Items;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "레시피 동기화 실패 → 로그인은 계속, 지금 레시피 사용");
            return await SafeOfflineAsync();
        }
    }

    private async Task<List<RecipeSyncItem>> SafeOfflineAsync()
    {
        try
        {
            return await _recipeSync.OfflineAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "레시피 상태를 읽지 못함 → recipeSync 빈 목록");
            return new List<RecipeSyncItem>();
        }
    }
}
