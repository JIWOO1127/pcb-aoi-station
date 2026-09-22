namespace Inspection.Contracts;

/// <summary>판정 결과. Invalid는 검사 자체를 못 한 경우(정렬 실패 등), 재검사 대상.</summary>
public enum Verdict { Pass, Fail, Invalid }

/// <summary>불량 종류. 스테이션 규칙 엔진이 탐지 결과와 레시피를 비교해 결정.</summary>
public enum DefectType
{
    Missing,        // 레시피 홀에 아무것도 없음
    WrongPart,      // 다른 종류가 꽂힘
    WireColorSwap,  // 전선인데 색이 다름
    Offset,         // 맞는 부품이 다른 홀에 (홀이 어긋남)
    Polarity,       // IC가 뒤집힘 (pin_side 반대)
    Extra           // 레시피에 없는 홀에 부품
}

/// <summary>브레드보드 홀 좌표. Row는 문자(A~U, 21행), Col은 숫자(0~13, 14열). 왼쪽 위가 A0.</summary>
public record Hole(char Row, int Col)
{
    public override string ToString() => $"{Row}{Col}";

    /// <summary>유효한 홀인지 (전원 레일 0,1,12,13에는 E,K,Q 행이 없음).</summary>
    public bool IsValid()
    {
        if (Row < 'A' || Row > 'U') return false;
        if (Col < 0 || Col > 13) return false;
        bool isPowerRail = Col is 0 or 1 or 12 or 13;
        bool isGap = Row is 'E' or 'K' or 'Q';
        return !(isPowerRail && isGap);
    }
}

/// <summary>추론 서비스가 보내는 탐지 하나. (본 것만, 판정 아님)</summary>
/// <param name="Cls">부품 종류 (resistance, ic_chip, red_wire ...)</param>
/// <param name="Confidence">탐지 신뢰도 0~1</param>
/// <param name="Holes">이 부품이 차지하는 홀 전체</param>
/// <param name="PinSide">IC만 "up"/"down", 그 외 null</param>
public record Detection(string Cls, double Confidence, List<Hole> Holes, string? PinSide);

/// <summary>레시피 부품 하나의 판정 결과.</summary>
public record PartResult(string PartId, string ExpectedClass, List<Hole> ExpectedHoles,
    string? DetectedClass, List<Hole>? DetectedHoles, string? PinSide,
    DefectType? Defect, Verdict Verdict);

/// <summary>보드 한 장의 검사 결과.</summary>
public record InspectionResult(Guid InspectionId, Verdict Verdict, int DefectCount,
    List<PartResult> Parts, string? ImageUrl, int LatencyMs, string? ErrorCode);