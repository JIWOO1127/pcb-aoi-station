using Inspection.Contracts;

namespace Inspection.Hmi.Services;

/// <summary>
/// 스테이션 서버에 검사를 요청한다.
/// 구현은 두 가지: MockStationClient(서버 없이 화면 개발용), HttpStationClient(실제).
/// </summary>
public interface IStationClient
{
    Task<InspectionResult> TriggerAsync(byte[] imageBytes, string productId,
        CancellationToken ct = default);
}