using Inspection.Contracts;
using Xunit;

namespace Inspection.Tests;

/// <summary>
/// 판정 규칙 엔진 테스트.
/// "레시피(정답지) + 탐지 결과 → 기대한 판정"이 나오는지 확인한다.
/// 모델·카메라·서버 없이 순수 로직만 검증한다.
/// </summary>
public class InspectionRulesTests
{
    private static Hole H(char row, int col) => new(row, col);

    private static RecipePart Recipe(string id, string cls, List<Hole> holes, string? pin = null)
        => new(id, cls, holes, pin);

    private static Detection Detect(string cls, List<Hole> holes, string? pin = null)
        => new(cls, 0.95, holes, pin);

    // 부품 3개짜리 기본 레시피
    private static List<RecipePart> BasicRecipe() => new()
    {
        Recipe("R1", "resistance_480", new() { H('A',0), H('A',1) }),
        Recipe("U1", "ic_chip",        new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
        Recipe("W1", "red_wire",       new() { H('E',0), H('E',1), H('E',2) }),
    };

    private static PartResult Part(List<PartResult> results, string partId)
        => results.First(r => r.PartId == partId);

    [Fact]
    public void 정상_모두_제자리()
    {
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            Detect("resistance_480", new() { H('A',0), H('A',1) }),
            Detect("ic_chip",        new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
            Detect("red_wire",       new() { H('E',0), H('E',1), H('E',2) }),
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.All(result, p => Assert.Equal(Verdict.Pass, p.Verdict));
    }

    [Fact]
    public void 오부품_제자리에_다른클래스()
    {
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            Detect("resistance_330", new() { H('A',0), H('A',1) }),  // 480 자리에 330
            Detect("ic_chip",        new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
            Detect("red_wire",       new() { H('E',0), H('E',1), H('E',2) }),
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.Equal(DefectType.WrongPart, Part(result, "R1").Defect);
    }

    [Fact]
    public void 전선색_제자리에_다른색전선()
    {
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            Detect("resistance_480", new() { H('A',0), H('A',1) }),
            Detect("ic_chip",        new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
            Detect("black_wire",     new() { H('E',0), H('E',1), H('E',2) }),  // 빨강 자리에 검정
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.Equal(DefectType.WireColorSwap, Part(result, "W1").Defect);
    }

    [Fact]
    public void IC방향_핀이_반대()
    {
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            Detect("resistance_480", new() { H('A',0), H('A',1) }),
            Detect("ic_chip",        new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "down"),
            Detect("red_wire",       new() { H('E',0), H('E',1), H('E',2) }),
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.Equal(DefectType.Polarity, Part(result, "U1").Defect);
    }

    [Fact]
    public void 위치_같은부품이_다른홀에()
    {
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            // R1(480, 2홀)이 원래 A0,A1인데 B0,B1로 옮겨짐
            Detect("resistance_480", new() { H('B',0), H('B',1) }),
            Detect("ic_chip",        new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
            Detect("red_wire",       new() { H('E',0), H('E',1), H('E',2) }),
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.Equal(DefectType.Offset, Part(result, "R1").Defect);
    }

    [Fact]
    public void 누락_제자리에도_다른곳에도_없음()
    {
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            // R1 아예 없음
            Detect("ic_chip",  new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
            Detect("red_wire", new() { H('E',0), H('E',1), H('E',2) }),
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.Equal(DefectType.Missing, Part(result, "R1").Defect);
    }

    [Fact]
    public void 추가_레시피에_없는_홀에()
    {
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            Detect("resistance_480", new() { H('A',0), H('A',1) }),
            Detect("ic_chip",        new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
            Detect("red_wire",       new() { H('E',0), H('E',1), H('E',2) }),
            Detect("led",            new() { H('T',5), H('T',6) }),  // 레시피에 없음
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.Contains(result, p => p.Defect == DefectType.Extra);
    }

    [Fact]
    public void 누락과_추가_동시에()
    {
        // R1이 없어지고, 대신 다른 클래스(led)가 엉뚱한 홀에 → R1 누락 + led 추가
        var recipe = BasicRecipe();
        var detections = new List<Detection>
        {
            Detect("led",      new() { H('T',5), H('T',6) }),  // 크기·클래스 다름
            Detect("ic_chip",  new() { H('C',5), H('C',6), H('D',5), H('D',6) }, "up"),
            Detect("red_wire", new() { H('E',0), H('E',1), H('E',2) }),
        };
        var result = InspectionRules.Judge(recipe, detections);
        Assert.Equal(DefectType.Missing, Part(result, "R1").Defect);       // R1 누락
        Assert.Contains(result, p => p.Defect == DefectType.Extra);         // led 추가
    }
}