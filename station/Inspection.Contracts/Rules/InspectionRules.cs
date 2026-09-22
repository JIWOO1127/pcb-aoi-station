namespace Inspection.Contracts;

/// <summary>
/// 판정 규칙 엔진. 레시피(정답지)와 탐지 결과(본 것)를 비교해 부품별 판정을 낸다.
/// 부품이 차지하는 '홀 집합'이 얼마나 겹치는지로 매칭한다.
/// 순수 함수라 카메라·모델·DB 없이 단위 테스트로 개발한다.
/// </summary>
public static class InspectionRules
{
    // 홀 집합이 완전히 같으면 정상 위치로 본다. (팀 규칙에 따라 '겹침 비율'로 완화 가능)
    private const double MinOverlapRatio = 1.0;

    private static bool IsWire(string cls) =>
        cls is "red_wire" or "black_wire" or "white_wire";

    public static List<PartResult> Judge(
        IReadOnlyList<RecipePart> recipe,
        IReadOnlyList<Detection> detections)
    {
        var results = new List<PartResult>();
        var usedDetections = new HashSet<Detection>();

        foreach (var part in recipe)
        {
            // 이 레시피 부품의 홀과 가장 많이 겹치는 탐지를 찾는다 (종류 무시하고 위치로)
            Detection? best = null;
            double bestOverlap = 0;
            foreach (var det in detections)
            {
                if (usedDetections.Contains(det)) continue;
                double overlap = OverlapRatio(part.Holes, det.Holes);
                if (overlap > bestOverlap) { bestOverlap = overlap; best = det; }
            }

            // 겹침이 거의 없으면 짝이 아닌 것으로 본다
            if (best is not null && bestOverlap > 0) usedDetections.Add(best);
            var matched = bestOverlap > 0 ? best : null;

            var defect = Classify(part, matched, bestOverlap, detections);
            results.Add(new PartResult(
                part.PartId, part.ExpectedClass, part.Holes,
                matched?.Cls, matched?.Holes, matched?.PinSide,
                defect, defect is null ? Verdict.Pass : Verdict.Fail));
        }

        // 짝이 안 지어진 탐지 → 추가 부품
        foreach (var det in detections)
        {
            if (usedDetections.Contains(det)) continue;
            results.Add(new PartResult(
                $"EXTRA@{det.Holes.FirstOrDefault()}", "-", new List<Hole>(),
                det.Cls, det.Holes, det.PinSide, DefectType.Extra, Verdict.Fail));
        }

        return results;
    }

    // 레시피 홀 중 몇 %가 탐지 홀에 포함되는가 (0~1)
    private static double OverlapRatio(List<Hole> recipeHoles, List<Hole> detHoles)
    {
        if (recipeHoles.Count == 0) return 0;
        var detSet = detHoles.ToHashSet();
        int hit = recipeHoles.Count(h => detSet.Contains(h));
        return (double)hit / recipeHoles.Count;
    }

    private static DefectType? Classify(RecipePart part, Detection? det, double overlap,
        IReadOnlyList<Detection> allDetections)
    {
        // 1. 제자리에 아무것도 없음
        if (det is null)
        {
            bool foundElsewhere = allDetections.Any(d => d.Cls == part.ExpectedClass);
            return foundElsewhere ? DefectType.Offset : DefectType.Missing;
        }

        // 2. 종류가 다름 (위치보다 먼저: 자리에 엉뚱한 게 있으면 오부품)
        if (det.Cls != part.ExpectedClass)
        {
            bool bothWire = IsWire(part.ExpectedClass) && IsWire(det.Cls);
            return bothWire ? DefectType.WireColorSwap : DefectType.WrongPart;
        }

        // 3. 종류는 맞지만 홀이 완전히 일치하지 않음 → 위치
        if (overlap < MinOverlapRatio) return DefectType.Offset;

        // 4. IC 방향
        if (part.ExpectedPinSide is not null && det.PinSide != part.ExpectedPinSide)
            return DefectType.Polarity;

        // 5. 정상
        return null;
    }
}