using Inspection.Station.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inspection.Station.Controllers;

/// <summary>
/// 추론 서비스(Python) 연동이 잘 되는지 눈으로 확인해보기 위한 테스트용 엔드포인트.
/// 실제 검사 흐름(POST /api/trigger)에는 아직 연결 안 됨.
/// 이건 "스테이션이 추론 서비스한테 이미지를 보내고, 응답(핀홀별 부품 정보)을
/// 잘 받아오는지"만 확인하는 용도.
/// </summary>
[ApiController]
[Route("api/inference-test")]
public class InferenceTestController : ControllerBase
{
    private readonly IInferenceClient _inferenceClient;

    public InferenceTestController(IInferenceClient inferenceClient)
    {
        _inferenceClient = inferenceClient;
    }

    [HttpPost]
    public async Task<ActionResult<InferResponse>> Post(IFormFile image, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await image.CopyToAsync(stream, ct);
        var result = await _inferenceClient.InferAsync(stream.ToArray(), ct);
        return Ok(result);
    }
}
