using Inspection.Contracts;

namespace Inspection.Hmi.Services;

/// <summary>
/// 스테이션 서버 없이 화면만 개발할 때 쓰는 가짜 클라이언트.
/// 호출할 때마다 정상 → 누락 → 오부품 → 전선색 → 검사 불가 순서로 돌려준다.
/// App.xaml.cs에서 HttpStationClient 대신 등록하면 서버 없이 화면을 볼 수 있다.
/// </summary>
public class MockStationClient : IStationClient
{
    private int _index;

    private static Hole H(string label) => Hole.Parse(label)!;

    private static readonly List<RecipePart> Recipe = new()
    {
        new("R1", "resistance470", new() { H("E9"), H("F9"), H("G9"), H("H9") }),
        new("U1", "ic_chip", new() { H("H6"), H("H7"), H("I6"), H("I7") }),
        new("W1", "red_wire", new() { H("H0"), H("H1"), H("H2") }),
    };

    public async Task<InspectionResult> TriggerAsync(byte[] imageBytes, string productId,
        CancellationToken ct = default)
    {
        await Task.Delay(800, ct);   // 처리 시간 흉내

        int scenario = _index++ % 5;

        if (scenario == 4)
            return new InspectionResult(Guid.NewGuid(), Verdict.Invalid, 0,
                new List<PartResult>(), null, 800, "MARKER_NOT_FOUND");

        // 시나리오별로 탐지 결과를 만들어 실제 규칙 엔진에 통과시킨다
        var detections = new List<HoleDetection>();
        void Cover(string cls, params string[] holes)
        {
            foreach (var h in holes) detections.Add(new HoleDetection(H(h), cls, 0.95));
        }

        // R1
        switch (scenario)
        {
            case 1: break;                                              // 누락
            case 2: Cover("led", "E9", "F9", "G9", "H9"); break;        // 오부품
            default: Cover("resistance470", "E9", "F9", "G9", "H9"); break;
        }
        // U1
        Cover("ic_chip", "H6", "H7", "I6", "I7");
        // W1
        if (scenario == 3) Cover("black_wire", "H0", "H1", "H2");       // 전선 색
        else Cover("red_wire", "H0", "H1", "H2");

        var parts = InspectionRules.Judge(Recipe, detections);
        int defects = parts.Count(p => p.Defect is not null);

        return new InspectionResult(Guid.NewGuid(),
            defects == 0 ? Verdict.Pass : Verdict.Fail,
            defects, parts, null, 800, null);
    }
}