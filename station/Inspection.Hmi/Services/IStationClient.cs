using Inspection.Contracts;

namespace Inspection.Hmi.Services;

public enum LoginStatus { Ok, InvalidCredentials, Unavailable }

/// <summary>로그인 시도 결과. Unavailable이면 HMI가 오프라인 로그인을 시도한다.</summary>
public record LoginOutcome(LoginStatus Status, LoginResult? Worker, string? Message = null);

/// <summary>
/// 스테이션 서버에 검사를 요청한다.
/// 구현은 두 가지: MockStationClient(서버 없이 화면 개발용), HttpStationClient(실제).
/// </summary>
public interface IStationClient
{
    Task<InspectionResult> TriggerAsync(byte[] imageBytes, string productId,
        CancellationToken ct = default);

    /// <summary>
    /// 사번 + PIN 로그인. 기본 구현은 "무조건 성공"이라 MockStationClient는 고치지 않아도 된다.
    /// </summary>
    Task<LoginOutcome> LoginAsync(string employeeId, string pin, CancellationToken ct = default)
        => Task.FromResult(new LoginOutcome(LoginStatus.Ok,
            new LoginResult(employeeId, "테스트 작업자", "WORKER", Offline: true)));

    Task<MesStatus?> GetMesStatusAsync(CancellationToken ct = default)
        => Task.FromResult<MesStatus?>(null);

}
