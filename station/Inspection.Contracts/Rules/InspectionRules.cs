namespace Inspection.Contracts;

/// <summary>
/// 판정 규칙 엔진. 레시피(정답지)와 탐지 결과(본 것)를 비교해 부품별 판정을 낸다.
/// "홀 목록이 같고 클래스가 같으면 같은 부품"으로 본다.
///
/// 판정 순서(레시피 부품마다):
///  1) 제자리에 같은 홀 목록의 탐지가 있음
///       - 클래스 같음 → (전선이면 색까지, IC면 방향까지 확인) → 정상 또는 전선색/방향
///       - 클래스 다름 → 오부품
///  2) 제자리에 탐지가 없음
///       - 같은 홀 '개수' + 같은 클래스가 다른 곳에 있음 → 위치 (그 탐지를 이 부품 것으로 사용)
///       - 없음 → 누락
///  3) 어느 레시피에도 매칭 안 된 탐지 → 추가
/// </summary>
public static class InspectionRules
{
    private static bool IsWire(string cls) =>
        cls is "red_wire" or "black_wire" or "white_wire";

    // 홀 목록이 완전히 같은지 (순서 무관)
    private static bool SameHoles(List<Hole> a, List<Hole> b)
        => a.Count == b.Count && a.ToHashSet().SetEquals(b);

    public static List<PartResult> Judge(
        IReadOnlyList<RecipePart> recipe,
        IReadOnlyList<Detection> detections)
    {
        var results = new List<PartResult>();
        var used = new HashSet<Detection>();   // 이미 어떤 부품에 배정된 탐지

        foreach (var part in recipe)
        {
            // 1) 제자리(같은 홀 목록)에 있는 탐지 찾기
            var atPlace = detections.FirstOrDefault(
                d => !used.Contains(d) && SameHoles(d.Holes, part.Holes));

            if (atPlace is not null)
            {
                used.Add(atPlace);
                var defect = ClassifyAtPlace(part, atPlace);
                results.Add(Build(part, atPlace, defect));
                continue;
            }

            // 2) 제자리에 없음 → 다른 곳에 '같은 홀 개수 + 같은 클래스'가 있나 (위치)
            var moved = detections.FirstOrDefault(
                d => !used.Contains(d)
                     && d.Cls == part.ExpectedClass
                     && d.Holes.Count == part.Holes.Count);

            if (moved is not null)
            {
                used.Add(moved);
                results.Add(Build(part, moved, DefectType.Offset));
            }
            else
            {
                // 3) 없으면 누락
                results.Add(Build(part, null, DefectType.Missing));
            }
        }

        // 4) 남은(매칭 안 된) 탐지 → 추가
        foreach (var d in detections)
        {
            if (used.Contains(d)) continue;
            results.Add(new PartResult(
                $"EXTRA@{d.Holes.FirstOrDefault()}", "-", new List<Hole>(),
                d.Cls, d.Holes, d.PinSide, DefectType.Extra, Verdict.Fail));
        }

        return results;
    }

    // 제자리에 탐지가 있을 때의 판정
    private static DefectType? ClassifyAtPlace(RecipePart part, Detection det)
    {
        // 클래스가 다름
        if (det.Cls != part.ExpectedClass)
        {
            bool bothWire = IsWire(part.ExpectedClass) && IsWire(det.Cls);
            return bothWire ? DefectType.WireColorSwap : DefectType.WrongPart;
        }
        // 클래스 같음 + IC 방향 확인
        if (part.ExpectedPinSide is not null && det.PinSide != part.ExpectedPinSide)
            return DefectType.Polarity;
        // 정상
        return null;
    }

    private static PartResult Build(RecipePart part, Detection? det, DefectType? defect)
        => new(part.PartId, part.ExpectedClass, part.Holes,
               det?.Cls, det?.Holes, det?.PinSide,
               defect, defect is null ? Verdict.Pass : Verdict.Fail);
}