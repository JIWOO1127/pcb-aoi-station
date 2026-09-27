namespace Inspection.Contracts;

/// <summary>판정 결과. Invalid는 검사 자체를 못 한 경우(정렬 실패 등), 재검사 대상.</summary>
public enum Verdict { Pass, Fail, Invalid }

/// <summary>불량 종류. 스테이션 규칙 엔진이 레시피와 탐지 결과를 비교해 결정.</summary>
public enum DefectType
{
    Missing,        // 레시피 홀이 전부 비어 있음
    WrongPart,      // 레시피 홀에 다른 종류가 있음
    WireColorSwap,  // 전선인데 색이 다름
    Offset,         // 레시피 홀 일부만 덮임 (살짝 어긋남)
    Extra           // 레시피에 없는 홀에 부품이 있음
}

/// <summary>브레드보드 핀홀. Row는 문자(A~U, 21행), Col은 숫자(0~13, 14열). 왼쪽 위가 A0.</summary>
public record Hole(char Row, int Col)
{
    public override string ToString() => $"{Row}{Col}";

    /// <summary>"J9" 같은 라벨을 Hole로. 형식이 틀리면 null.</summary>
    public static Hole? Parse(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length < 2) return null;
        char row = char.ToUpperInvariant(label[0]);
        return int.TryParse(label[1..], out int col) ? new Hole(row, col) : null;
    }

    /// <summary>실제 존재하는 홀인지 (전원 레일 0,1,12,13에는 E,K,Q 행이 없음).</summary>
    public bool IsValid()
    {
        if (Row < 'A' || Row > 'U') return false;
        if (Col < 0 || Col > 13) return false;
        bool isPowerRail = Col is 0 or 1 or 12 or 13;
        bool isGap = Row is 'E' or 'K' or 'Q';
        return !(isPowerRail && isGap);
    }
}

/// <summary>추론 서비스가 알려준 핀홀 하나의 상태. (본 것만, 판정 아님)</summary>
/// <param name="Hole">핀홀</param>
/// <param name="Cls">그 홀을 덮고 있는 부품 종류</param>
/// <param name="Confidence">탐지 신뢰도 0~1</param>
public record HoleDetection(Hole Hole, string Cls, double Confidence);

/// <summary>레시피 부품 하나의 판정 결과.</summary>
/// <param name="PartId">레시피 부품 ID (R1, U1, W1). 추가 부품이면 "EXTRA@홀"</param>
/// <param name="ExpectedClass">있어야 할 종류. 추가 부품이면 "-"</param>
/// <param name="ExpectedHoles">있어야 할 홀 전체</param>
/// <param name="DetectedClass">레시피 홀에서 실제로 발견된 종류. 전부 비었으면 null</param>
/// <param name="CoveredHoles">레시피 홀 중 실제로 덮여 있던 홀</param>
/// <param name="Defect">불량 종류. 정상이면 null</param>
public record PartResult(string PartId, string ExpectedClass, List<Hole> ExpectedHoles,
    string? DetectedClass, List<Hole> CoveredHoles, DefectType? Defect, Verdict Verdict);

/// <summary>보드 한 장의 검사 결과.</summary>
/// <param name="Overlay">실제 사진 위에 그릴 정보. 추론 서비스가 주지 않았거나 판정 불가면 null</param>
public record InspectionResult(Guid InspectionId, Verdict Verdict, int DefectCount,
    List<PartResult> Parts, string? ImageUrl, int LatencyMs, string? ErrorCode,
    InspectionOverlay? Overlay = null);