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
    /// <summary>
    /// 이 신뢰도 미만인 탐지는 판정에서 뺀다.
    /// Camo 실측: 같은 LED를 한 칸 옆에서 중복으로 잡은 박스가 0.31,
    /// 정상 부품 중 가장 낮은 것이 R3의 0.64라서 그 사이인 0.5로 둔다.
    /// </summary>
    private const double MinConfidence = 0.5;

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
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // 호출한 쪽이 취소한 게 아니라 HttpClient 제한 시간이 지남
            _log.LogError(ex, "추론 서비스 시간 초과");
            return Invalid("INFERENCE_TIMEOUT");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            // 추론 서비스가 오류 응답을 줌 (크롭·홀 검출 실패 등 → 보드를 인식하지 못함)
            _log.LogError(ex, "추론 서비스 오류 응답 {Status}", (int)ex.StatusCode!.Value);
            return Invalid("BOARD_NOT_RECOGNIZED");
        }
        catch (HttpRequestException ex)
        {
            // 연결 자체 실패 (추론 서비스가 꺼져 있거나 주소가 틀림)
            _log.LogError(ex, "추론 서비스에 연결할 수 없음");
            return Invalid("INFERENCE_UNAVAILABLE");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 그 밖의 실패(응답 형식 오류 등)도 500 대신 "판정 불가"로 안내한다.
            // 호출한 쪽이 취소한 경우만 위로 올려보낸다.
            _log.LogError(ex, "추론 서비스 호출 실패 (예상하지 못한 오류)");
            return Invalid("INFERENCE_UNAVAILABLE");
        }

        InspectionResult Invalid(string errorCode) =>
            new(inspectionId, Verdict.Invalid, 0,
                new List<PartResult>(), null, (int)sw.ElapsedMilliseconds, errorCode);

        // 3. 응답을 Contracts 형식으로 변환 (신뢰도 낮은 탐지·잘못된 홀 라벨은 버린다)
        var detections = new List<HoleDetection>();
        foreach (var p in infer.Parts)
        {
            if (p.Confidence < MinConfidence)
            {
                _log.LogInformation("낮은 신뢰도 탐지 제외: {Pinhole} {Class} {Confidence:F2}",
                    p.Pinhole, p.Class, p.Confidence);
                continue;
            }

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

        // 추론 서비스가 정렬된 사진·좌표를 줬으면 HMI가 실제 사진 위에 그린다 (없으면 null → 미니어처)
        return new InspectionResult(
            inspectionId,
            defectCount == 0 ? Verdict.Pass : Verdict.Fail,
            defectCount, parts, null, (int)sw.ElapsedMilliseconds, null,
            Overlay: infer.Overlay);
    }
}