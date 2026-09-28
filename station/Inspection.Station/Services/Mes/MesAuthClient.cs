using System.Net;
using System.Net.Http.Json;
using Inspection.Contracts;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Services.Mes;

/// <summary>
/// MES 작업자 로그인 API(POST /api/auth/login, MVP-26)를 호출한다.
/// HMI는 MES를 직접 부르지 않고 스테이션을 거친다 (README 원칙: HMI는 스테이션만 안다).
/// </summary>
public class MesAuthClient
{
    public enum Outcome { Ok, InvalidCredentials, Unavailable }

    private readonly HttpClient _http;
    private readonly MesOptions _options;
    private readonly ILogger<MesAuthClient> _log;

    public MesAuthClient(HttpClient http, IOptions<MesOptions> options, ILogger<MesAuthClient> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    // MES 응답 모양 (docs/api-contract.md, 인증 API)
    private record MesUser(string EmployeeId, string Name, string Role);
    private record MesLoginResponse(string AccessToken, string TokenType, int ExpiresIn, MesUser User);

    /// <returns>
    /// AccessToken: MES 작업자 JWT. 스테이션 안에서 로그인 직후 레시피 동기화에만 쓰고 저장하지 않는다
    /// (HMI로도 보내지 않는다). 검사 기록은 여전히 스테이션 키로 보낸다.
    /// </returns>
    public async Task<(Outcome Outcome, LoginResult? User, string? AccessToken)> LoginAsync(
        LoginRequest request, CancellationToken ct)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(_options.LoginPath,
                new { employeeId = request.EmployeeId, pin = request.Pin }, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                return (Outcome.InvalidCredentials, null, null);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("MES 로그인 오류 {Code}", (int)response.StatusCode);
                return (Outcome.Unavailable, null, null);
            }

            var body = await response.Content.ReadFromJsonAsync<MesLoginResponse>(cancellationToken: ct);
            if (body?.User is null) return (Outcome.Unavailable, null, null);

            return (Outcome.Ok, new LoginResult(body.User.EmployeeId, body.User.Name, body.User.Role, Offline: false),
                body.AccessToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _log.LogWarning("MES 로그인 연결 실패: {Message}", ex.Message);
            return (Outcome.Unavailable, null, null);
        }
    }
}
