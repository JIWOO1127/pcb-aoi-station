using Inspection.Contracts;

namespace Inspection.Hmi.ViewModels;

/// <summary>미니어처의 홀 점 하나 (배경 격자).</summary>
public record BoardCell(double X, double Y, double Size, string Label);

/// <summary>
/// 보드 위에 그리는 표시의 종류.
///
/// v13: 불량 "유형"이 아니라 작업자가 해야 할 "동작"으로 나눈다.
/// 작업자는 불량 이름보다 "손으로 무엇을 할지"가 필요하고,
/// 색 의미를 외울 필요 없이 모양만 보고 동작을 알 수 있게 하기 위해서다.
/// 색은 빨강 하나, 구분은 모양으로 한다.
/// </summary>
public enum PartShapeKind
{
    Normal,     // 정상: 연한 윤곽선 (배치 참고용)
    Insert,     // 꽂기 (누락): 빨간 점선 빈 윤곽 — "여기가 비었다"
    Replace,    // 교체 (다른 부품·전선 색): 빨간 실선 윤곽 — "이걸 바꿔라"
    Remove,     // 빼기 (추가 부품): 빨간 X — "이걸 빼라"
    Move        // 옮기기 (위치): 점선(가야 할 자리) + 빨간 점(지금 덮인 홀)
}

/// <summary>부품 윤곽 안에 찍는 점 (옮기기에서 "지금 덮인 홀"). 좌표는 부품 윤곽 기준.</summary>
public record HoleDot(double X, double Y, double Size);

/// <summary>미니어처의 부품 하나. 차지하는 홀들을 감싸는 사각형으로 그린다.</summary>
/// <param name="Badge">불량 번호. 오른쪽 카드 번호와 같다. 정상이면 빈 문자열</param>
/// <param name="Label">윤곽 안 글자. 정상은 부품 ID, 불량은 동작("꽂기")</param>
/// <param name="Class">아이콘을 고르는 부품 종류 (resistance330, led ...). 추가 부품이면 실제로 탐지된 종류</param>
/// <param name="IconAngle">아이콘 회전. 아이콘은 가로로 그려 두고, 세로로 꽂힌 부품이면 90도 돌린다</param>
public record BoardPart(string PartId, double X, double Y, double Width, double Height,
    PartShapeKind Kind, string Badge, string Label, string Tooltip, List<HoleDot> Dots,
    string Class, double IconAngle)
{
    public static PartShapeKind KindOf(DefectType? defect) => defect switch
    {
        null => PartShapeKind.Normal,
        DefectType.Missing => PartShapeKind.Insert,
        DefectType.Extra => PartShapeKind.Remove,
        DefectType.Offset => PartShapeKind.Move,
        _ => PartShapeKind.Replace          // WrongPart, WireColorSwap
    };

    public static string VerbOf(PartShapeKind kind) => kind switch
    {
        PartShapeKind.Insert => "꽂기",
        PartShapeKind.Replace => "교체",
        PartShapeKind.Remove => "빼기",
        PartShapeKind.Move => "옮기기",
        _ => ""
    };

    /// <param name="defectIndex">불량 순번(0부터). 정상이면 -1</param>
    public static BoardPart? From(PartResult p, int defectIndex,
        Func<DefectType?, string> describe, Func<string?, string> korean)
    {
        // 레시피 홀이 있으면 그 자리(=있어야 할 자리)에, 추가 부품이면 실제로 덮인 자리에 그린다
        var holes = p.ExpectedHoles.Count > 0 ? p.ExpectedHoles : p.CoveredHoles;
        if (holes.Count == 0) return null;

        const double pad = BoardLayout.HoleSize;
        double left = holes.Min(h => BoardLayout.X(h.Col)) - pad;
        double top = holes.Min(h => BoardLayout.Y(h.Row)) - pad;
        double width = holes.Max(h => BoardLayout.X(h.Col)) + pad - left;
        double height = holes.Max(h => BoardLayout.Y(h.Row)) + pad - top;

        var kind = KindOf(p.Defect);
        string badge = defectIndex >= 0 ? (defectIndex + 1).ToString() : "";
        string label = kind == PartShapeKind.Normal ? p.PartId : VerbOf(kind);

        // 옮기기: 레시피 홀 중 지금 덮인 홀에 점을 찍는다 (어디까지 걸쳐 있는지)
        var dots = new List<HoleDot>();
        if (kind == PartShapeKind.Move)
        {
            const double d = BoardLayout.HoleSize + 3;
            foreach (var h in p.CoveredHoles)
                dots.Add(new HoleDot(BoardLayout.X(h.Col) - left - d / 2, BoardLayout.Y(h.Row) - top - d / 2, d));
        }

        string tooltip = $"{p.PartId} · {describe(p.Defect)}\n"
                       + $"있어야 할 것: {korean(p.ExpectedClass)}\n"
                       + $"탐지된 것: {korean(p.DetectedClass)}\n"
                       + $"홀: {string.Join(", ", holes)}";

        // 아이콘: 있어야 할 부품 모양 (꽂기라면 "여기에 이 모양을 꽂아라"). 추가 부품은 실제로 있는 것
        string cls = p.ExpectedClass is "-" or "" ? (p.DetectedClass ?? "") : p.ExpectedClass;
        double angle = height > width ? 90 : 0;

        return new BoardPart(p.PartId, left, top, width, height, kind, badge, label, tooltip, dots, cls, angle);
    }
}

/// <summary>
/// 실제 사진 위에 불량을 그릴 때 쓰는 부품 표시 계산.
/// 미니어처와 같은 BoardPart를 만들되, 좌표는 BoardLayout 대신 추론 서비스가 준 구멍 픽셀 위치로 계산한다.
/// 그래서 동작 표시(꽂기·교체·빼기·옮기기)와 번호 배지를 사진과 미니어처에서 똑같이 쓸 수 있다.
/// </summary>
public static class PhotoLayout
{
    /// <param name="locate">구멍 → 화면 좌표 (없으면 null)</param>
    /// <param name="pad">구멍 중심에서 바깥으로 넓힐 여백 (보통 구멍 간격의 절반)</param>
    public static BoardPart? From(PartResult p, int defectIndex, Func<Hole, (double X, double Y)?> locate,
        double pad, double dotSize, Func<DefectType?, string> describe, Func<string?, string> korean)
    {
        var holes = p.ExpectedHoles.Count > 0 ? p.ExpectedHoles : p.CoveredHoles;
        var points = holes.Select(locate).Where(v => v is not null).Select(v => v!.Value).ToList();
        if (points.Count == 0) return null;

        double left = points.Min(v => v.X) - pad;
        double top = points.Min(v => v.Y) - pad;
        double width = points.Max(v => v.X) + pad - left;
        double height = points.Max(v => v.Y) + pad - top;

        var kind = BoardPart.KindOf(p.Defect);
        string badge = defectIndex >= 0 ? (defectIndex + 1).ToString() : "";
        string label = kind == PartShapeKind.Normal ? p.PartId : BoardPart.VerbOf(kind);

        var dots = new List<HoleDot>();
        if (kind == PartShapeKind.Move)
        {
            foreach (var h in p.CoveredHoles)
                if (locate(h) is { } c)
                    dots.Add(new HoleDot(c.X - left - dotSize / 2, c.Y - top - dotSize / 2, dotSize));
        }

        string tooltip = $"{p.PartId} · {describe(p.Defect)}\n"
                       + $"있어야 할 것: {korean(p.ExpectedClass)}\n"
                       + $"탐지된 것: {korean(p.DetectedClass)}\n"
                       + $"홀: {string.Join(", ", holes)}";

        string cls = p.ExpectedClass is "-" or "" ? (p.DetectedClass ?? "") : p.ExpectedClass;
        return new BoardPart(p.PartId, left, top, width, height, kind, badge, label, tooltip, dots, cls,
            height > width ? 90 : 0);
    }
}

/// <summary>"모델이 본 것" 보기에서 그리는 YOLO 박스 하나 (화면 좌표).</summary>
public record ModelBox(double X, double Y, double Width, double Height, string Label, string Color);

/// <summary>사유 영역에 표시할 불량 한 건.</summary>
/// <param name="Badge">보드 위 번호와 같은 값</param>
/// <param name="Title">"R1 누락" — 무엇이 문제인가 (재검사에서 새로 생긴 불량이면 표시가 붙는다)</param>
/// <param name="Detail">"저항 470Ω → 없음" — 기대와 실제</param>
/// <param name="Action">"E9,F9에 저항 470Ω을 꽂으세요" — 어떻게 고치는가</param>
public record DefectCard(string Badge, string Title, string Detail, string Action);
