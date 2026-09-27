using Inspection.Contracts;

namespace Inspection.Station.Services.Mes;

/// <summary>
/// MES로 보내는 검사 기록 한 건 (POST /api/inspections의 본문).
/// 규격은 docs/api-contract.md의 "검사 기록 API". MES 담당자와 확정되면 이 파일만 고치면 된다.
///
/// Contracts의 InspectionResult를 그대로 보내지 않고 따로 두는 이유:
///   - MES가 알아야 할 것(스테이션, 제품, 작업자, 촬영 시각)이 결과에는 없다
///   - enum과 홀을 사람이 읽는 글자로 보내야 Java 쪽이 편하다 ("FAIL", "Q10")
///   - MES 규격이 바뀌어도 스테이션·HMI의 내부 모델은 흔들리지 않는다
/// </summary>
public record MesInspectionRecord(
    Guid InspectionId,          // 스테이션이 만든 ID. MES는 같은 ID가 다시 오면 저장하지 않는다 (재전송 중복 방지)
    string StationId,
    string ProductId,
    string? WorkerId,           // HMI 로그인 전에는 null
    string Verdict,             // "PASS" | "FAIL" | "INVALID"
    int DefectCount,
    string? ErrorCode,          // 판정 불가 이유 (INVALID일 때)
    int LatencyMs,
    DateTimeOffset CapturedAt,  // 검사 시각 (스테이션 기준, +09:00)
    List<MesPartRecord> Parts)
{
    public static MesInspectionRecord From(InspectionResult r, string stationId, string productId,
        string? workerId, DateTimeOffset capturedAt) =>
        new(r.InspectionId, stationId, productId, workerId,
            r.Verdict.ToString().ToUpperInvariant(),
            r.DefectCount, r.ErrorCode, r.LatencyMs, capturedAt,
            r.Parts.Select(MesPartRecord.From).ToList());
}

/// <summary>부품 하나의 판정. 홀은 "Q10" 같은 글자 목록으로 보낸다.</summary>
public record MesPartRecord(
    string PartId,              // "R1", 추가 부품은 "EXTRA@B5"
    string ExpectedClass,       // 추가 부품이면 "-"
    string? DetectedClass,      // 전부 비었으면 null
    string? DefectType,         // null(정상) | "MISSING" | "WRONG_PART" | "WIRE_COLOR_SWAP" | "OFFSET" | "EXTRA"
    List<string> ExpectedHoles,
    List<string> CoveredHoles)
{
    public static MesPartRecord From(PartResult p) =>
        new(p.PartId, p.ExpectedClass, p.DetectedClass, ToCode(p.Defect),
            p.ExpectedHoles.Select(h => h.ToString()).ToList(),
            p.CoveredHoles.Select(h => h.ToString()).ToList());

    private static string? ToCode(Inspection.Contracts.DefectType? d) => d switch
    {
        null => null,
        Inspection.Contracts.DefectType.Missing => "MISSING",
        Inspection.Contracts.DefectType.WrongPart => "WRONG_PART",
        Inspection.Contracts.DefectType.WireColorSwap => "WIRE_COLOR_SWAP",
        Inspection.Contracts.DefectType.Offset => "OFFSET",
        Inspection.Contracts.DefectType.Extra => "EXTRA",
        _ => d.ToString()!.ToUpperInvariant()
    };
}
