using Inspection.Contracts;
using Xunit;

namespace Inspection.Tests;

/// <summary>
/// 판정 규칙 엔진 테스트.
/// "레시피 + 핀홀별 탐지 결과 → 기대한 판정"이 나오는지 확인한다.
/// 모델·카메라·서버 없이 순수 로직만 검증한다.
/// </summary>
public class InspectionRulesTests
{
    private static Hole H(string label) => Hole.Parse(label)!;

    private static RecipePart Recipe(string id, string cls, params string[] holes)
        => new(id, cls, holes.Select(H).ToList());

    // 홀 여러 개를 같은 부품으로 덮기
    private static IEnumerable<HoleDetection> Cover(string cls, params string[] holes)
        => holes.Select(h => new HoleDetection(H(h), cls, 0.95));

    // 부품 3개짜리 기본 레시피
    private static List<RecipePart> BasicRecipe() => new()
    {
        Recipe("R1", "resistance470", "E9", "F9", "G9", "H9"),
        Recipe("U1", "ic_chip", "H6", "H7", "I6", "I7"),
        Recipe("W1", "red_wire", "H0", "H1", "H2"),
    };

    private static PartResult Part(List<PartResult> results, string partId)
        => results.First(r => r.PartId == partId);

    [Fact]
    public void 정상_레시피대로_전부_덮임()
    {
        var detections = Cover("resistance470", "E9", "F9", "G9", "H9")
            .Concat(Cover("ic_chip", "H6", "H7", "I6", "I7"))
            .Concat(Cover("red_wire", "H0", "H1", "H2")).ToList();

        var result = InspectionRules.Judge(BasicRecipe(), detections);

        Assert.All(result, p => Assert.Equal(Verdict.Pass, p.Verdict));
    }

    [Fact]
    public void 누락_레시피홀이_전부_비어있음()
    {
        var detections = Cover("ic_chip", "H6", "H7", "I6", "I7")
            .Concat(Cover("red_wire", "H0", "H1", "H2")).ToList();   // R1 없음

        var result = InspectionRules.Judge(BasicRecipe(), detections);

        Assert.Equal(DefectType.Missing, Part(result, "R1").Defect);
    }

    [Fact]
    public void 오부품_레시피홀에_다른종류()
    {
        var detections = Cover("resistance330", "E9", "F9", "G9", "H9")   // 470 자리에 330
            .Concat(Cover("ic_chip", "H6", "H7", "I6", "I7"))
            .Concat(Cover("red_wire", "H0", "H1", "H2")).ToList();

        var result = InspectionRules.Judge(BasicRecipe(), detections);

        var r1 = Part(result, "R1");
        Assert.Equal(DefectType.WrongPart, r1.Defect);
        Assert.Equal("resistance330", r1.DetectedClass);
    }

    [Fact]
    public void 전선색_빨강자리에_검정()
    {
        var detections = Cover("resistance470", "E9", "F9", "G9", "H9")
            .Concat(Cover("ic_chip", "H6", "H7", "I6", "I7"))
            .Concat(Cover("black_wire", "H0", "H1", "H2")).ToList();

        var result = InspectionRules.Judge(BasicRecipe(), detections);

        Assert.Equal(DefectType.WireColorSwap, Part(result, "W1").Defect);
    }

    [Fact]
    public void 위치_레시피홀_일부만_덮임()
    {
        // 저항이 한 칸 밀려서 E9는 비고 I9까지 걸침
        var detections = Cover("resistance470", "F9", "G9", "H9", "I9")
            .Concat(Cover("ic_chip", "H6", "H7", "I6", "I7"))
            .Concat(Cover("red_wire", "H0", "H1", "H2")).ToList();

        var result = InspectionRules.Judge(BasicRecipe(), detections);

        Assert.Equal(DefectType.Offset, Part(result, "R1").Defect);
    }

    [Fact]
    public void 추가_레시피에_없는_홀에_부품()
    {
        var detections = Cover("resistance470", "E9", "F9", "G9", "H9")
            .Concat(Cover("ic_chip", "H6", "H7", "I6", "I7"))
            .Concat(Cover("red_wire", "H0", "H1", "H2"))
            .Concat(Cover("led", "T5", "T6")).ToList();              // 레시피에 없음

        var result = InspectionRules.Judge(BasicRecipe(), detections);

        Assert.Contains(result, p => p.Defect == DefectType.Extra);
    }

    [Fact]
    public void 같은종류_저항두개는_홀위치로_구분된다()
    {
        var recipe = new List<RecipePart>
        {
            Recipe("R2", "resistance330", "N3", "O3", "P3", "Q3"),
            Recipe("R3", "resistance330", "N8", "O8", "P8", "Q8"),
        };
        var detections = Cover("resistance330", "N3", "O3", "P3", "Q3")
            .Concat(Cover("resistance330", "N8", "O8", "P8", "Q8")).ToList();

        var result = InspectionRules.Judge(recipe, detections);

        Assert.All(result, p => Assert.Equal(Verdict.Pass, p.Verdict));
    }

    [Fact]
    public void 홀라벨_파싱과_유효성()
    {
        Assert.Equal(new Hole('J', 9), Hole.Parse("J9"));
        Assert.Equal(new Hole('U', 13), Hole.Parse("U13"));
        Assert.Null(Hole.Parse("9J"));
        Assert.False(new Hole('E', 0).IsValid());    // 전원 레일에 E행 없음
        Assert.True(new Hole('E', 5).IsValid());
    }
}