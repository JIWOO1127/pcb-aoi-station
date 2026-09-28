using Inspection.Contracts;

namespace Inspection.Hmi.Services;

public enum LoginStatus { Ok, InvalidCredentials, Unavailable }

/// <summary>
/// 로그인 시도 결과. Unavailable이면 HMI가 오프라인 로그인을 시도한다.
/// RecipeSync: 스테이션이 로그인 때 MES 레시피를 맞춰 본 결과 (/api/login 200·503 응답의 recipeSync). 모르면 빈 목록.
/// </summary>
public record LoginOutcome(LoginStatus Status, LoginResult? Worker, string? Message = null,
    IReadOnlyList<RecipeSyncItem>? RecipeSync = null);

/// <summary>
/// /api/login 응답 recipeSync[] 한 줄. status: UPDATED | UNCHANGED | OFFLINE | NOT_FOUND | INVALID.
/// (스테이션 전용 필드라 Inspection.Contracts가 아니라 HMI 안에 둔다)
/// </summary>
public record RecipeSyncItem(string ProductId, string Status, int? Version, Guid? RecipeId,
    string? ApprovedBy, DateTimeOffset? ApprovedAt);

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
