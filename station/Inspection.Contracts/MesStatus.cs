namespace Inspection.Contracts;

/// <summary>
/// 스테이션 → MES 전송 상태. HMI 헤더의 MES 점에 쓴다.
/// </summary>
/// <param name="Enabled">MES 전송을 켰는지 (appsettings Mes:Enabled)</param>
/// <param name="Connected">마지막 전송 시도가 성공했는지. 아직 시도 전이면 null</param>
/// <param name="Pending">아직 MES에 못 보낸 검사 기록 수 (스테이션 PC에 쌓여 있음)</param>
/// <param name="LastSuccessAt">마지막으로 전송에 성공한 시각</param>
/// <param name="LastError">마지막 실패 이유 (사람이 읽는 한 줄)</param>
public record MesStatus(bool Enabled, bool? Connected, int Pending,
    DateTimeOffset? LastSuccessAt, string? LastError);
