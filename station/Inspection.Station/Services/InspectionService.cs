using System.Diagnostics;
using Inspection.Contracts;

namespace Inspection.Station.Services;

/// <summary>
/// 검사 한 번의 흐름을 지휘한다.
/// 사진 → 추론 서비스 호출 → 레시피와 비교해 판정 → 결과 조립.
/// 판정 규칙 자체는 Contracts의 InspectionRules가 갖고 있다.
/// </summary>
public class InspectionService
{
    private readonly IInferenceClient _inference;
    private readonly IRecipeProvider _recipes;
    private readonly ILogger<InspectionService> _log;

    public InspectionService(IInferenceClient inference, IRecipeProvider recipes,
        ILogger<InspectionService> log)
    {
        _inference = inference;
        _recipes = recipes;
        _log = log;
    }

    public async Task<InspectionResult> InspectAsync(
        byte[] jpegBytes, string productId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var inspectionId = Guid.NewGuid();

        // 1. 레시피 (정답지)
        var recipe = await _recipes.GetAsync(productId, ct);

        // 2. 추론 서비스 호출 → 핀홀별 부품
        InferResponse infer;
        try
        {
            infer = await _inference.InferAsync(jpegBytes, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "추론 서비스 호출 실패");
            return new InspectionResult(inspectionId, Verdict.Invalid, 0,
                new List<PartResult>(), null, (int)sw.ElapsedMilliseconds,
                "INFERENCE_UNAVAILABLE");
        }

        // 3. 응답을 Contracts 형식으로 변환 (잘못된 홀 라벨은 버린다)
        var detections = new List<HoleDetection>();
        foreach (var p in infer.Parts)
        {
            var hole = Hole.Parse(p.Pinhole);
            if (hole is null || !hole.IsValid())
            {
                _log.LogWarning("알 수 없는 핀홀 라벨: {Label}", p.Pinhole);
                continue;
            }
            detections.Add(new HoleDetection(hole, p.Class, p.Confidence));
        }

        // 4. 판정
        var parts = InspectionRules.Judge(recipe.Parts, detections);
        int defectCount = parts.Count(p => p.Defect is not null);

        return new InspectionResult(
            inspectionId,
            defectCount == 0 ? Verdict.Pass : Verdict.Fail,
            defectCount, parts, null, (int)sw.ElapsedMilliseconds, null);
    }
}