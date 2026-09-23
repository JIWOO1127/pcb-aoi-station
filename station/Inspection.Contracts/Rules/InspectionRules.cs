namespace Inspection.Contracts;

/// <summary>
/// 판정 규칙 엔진.
/// 추론 서비스는 "핀홀별로 어떤 부품이 덮고 있는지"를 준다.
/// 규칙 엔진은 레시피 부품마다 그 부품이 차지해야 할 홀들을 조회해서 판정한다.
///
/// 레시피 부품마다:
///   레시피 홀 중 덮인 홀이 하나도 없음        → 누락
///   덮인 홀의 종류가 기대와 다름              → 오부품 (전선끼리 색만 다르면 전선 색)
///   종류는 맞는데 레시피 홀 일부만 덮임        → 위치
///   레시피 홀이 전부 기대 종류로 덮임          → 정상
/// 마지막에 레시피에 없는 홀이 덮여 있으면      → 추가
///
/// 순수 함수라 카메라·모델·DB 없이 단위 테스트로 개발한다.
/// </summary>
public static class InspectionRules
{
    private static bool IsWire(string cls) =>
        cls is "red_wire" or "black_wire" or "white_wire";

    public static List<PartResult> Judge(
        IReadOnlyList<RecipePart> recipe,
        IReadOnlyList<HoleDetection> detections)
    {
        // 홀 → 그 홀을 덮은 부품
        var byHole = new Dictionary<Hole, HoleDetection>();
        foreach (var d in detections) byHole[d.Hole] = d;

        var usedHoles = new HashSet<Hole>();
        var results = new List<PartResult>();

        foreach (var part in recipe)
        {
            // 이 부품이 차지해야 할 홀 중, 실제로 덮여 있는 홀들
            var covered = new List<Hole>();
            var classes = new List<string>();
            foreach (var hole in part.Holes)
            {
                if (!byHole.TryGetValue(hole, out var det)) continue;
                covered.Add(hole);
                classes.Add(det.Cls);
                usedHoles.Add(hole);
            }

            var (defect, detectedClass) = Classify(part, covered, classes);
            results.Add(new PartResult(
                part.PartId, part.ExpectedClass, part.Holes,
                detectedClass, covered,
                defect, defect is null ? Verdict.Pass : Verdict.Fail));
        }

        // 레시피 어느 부품에도 속하지 않은 홀이 덮여 있으면 추가 부품
        var extras = detections
            .Where(d => !usedHoles.Contains(d.Hole))
            .GroupBy(d => d.Cls);   // 같은 종류끼리 한 줄로 묶어서 보기 좋게

        foreach (var group in extras)
        {
            var holes = group.Select(d => d.Hole).ToList();
            results.Add(new PartResult(
                $"EXTRA@{holes[0]}", "-", new List<Hole>(),
                group.Key, holes, DefectType.Extra, Verdict.Fail));
        }

        return results;
    }

    private static (DefectType?, string?) Classify(
        RecipePart part, List<Hole> covered, List<string> classes)
    {
        // 1. 아무 홀도 덮여 있지 않음
        if (covered.Count == 0) return (DefectType.Missing, null);

        // 덮인 홀에서 가장 많이 나온 종류를 대표로 본다
        string detected = classes.GroupBy(c => c)
                                 .OrderByDescending(g => g.Count())
                                 .First().Key;

        // 2. 종류가 기대와 다름
        if (detected != part.ExpectedClass)
        {
            bool bothWire = IsWire(part.ExpectedClass) && IsWire(detected);
            return (bothWire ? DefectType.WireColorSwap : DefectType.WrongPart, detected);
        }

        // 3. 종류는 맞는데 일부 홀만 덮임 → 위치가 어긋남
        if (covered.Count < part.Holes.Count) return (DefectType.Offset, detected);

        // 4. 정상
        return (null, detected);
    }
}