using Inspection.Contracts;
using Inspection.Station.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inspection.Station.Controllers;

[ApiController]
[Route("api")]
public class InspectionController : ControllerBase
{
    private readonly InspectionService _inspection;

    public InspectionController(InspectionService inspection) => _inspection = inspection;

    /// <summary>
    /// 검사 실행. 지금은 사진 파일을 직접 받는다.
    /// (카메라를 붙이면 이미지 없이 productId만 받고 스테이션이 직접 찍게 바꾼다.)
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<InspectionResult>> Trigger(
        IFormFile image, [FromForm] string productId, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = new { code = "NO_IMAGE", message = "이미지가 없습니다." } });

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);

        var result = await _inspection.InspectAsync(ms.ToArray(), productId, ct);
        return Ok(result);
    }
}