using Inspection.Contracts;
using Inspection.Station.Services.Mes;
using Microsoft.AspNetCore.Mvc;

namespace Inspection.Station.Controllers;

/// <summary>
/// POST /api/login  { employeeId, pin }
///   200 { employeeId, name, role, offline:false }
///   401 사번 또는 PIN이 틀림
///   503 MES에 연결할 수 없음 → HMI가 저장해 둔 정보로 오프라인 로그인을 시도한다
/// </summary>
[ApiController]
[Route("api")]
public class AuthController : ControllerBase
{
    private readonly MesAuthClient _mes;

    public AuthController(MesAuthClient mes) => _mes = mes;

    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { message = "사번과 PIN을 입력하세요." });

        var (outcome, user) = await _mes.LoginAsync(request, ct);
        return outcome switch
        {
            MesAuthClient.Outcome.Ok => Ok(user),
            MesAuthClient.Outcome.InvalidCredentials => Unauthorized(new { message = "사번 또는 PIN이 올바르지 않습니다." }),
            _ => StatusCode(503, new { code = "MES_UNAVAILABLE", message = "MES에 연결할 수 없습니다." })
        };
    }
}
