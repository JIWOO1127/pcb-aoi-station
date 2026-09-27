using Inspection.Contracts;
using Inspection.Station.Services;
using Inspection.Station.Services.Mes;
using Microsoft.AspNetCore.Mvc;

namespace Inspection.Station.Controllers;

[ApiController]
[Route("api")]
public class InspectionController : ControllerBase
{
    private readonly InspectionService _inspection;
    private readonly MesOutbox _mes;

    public InspectionController(InspectionService inspection, MesOutbox mes)
    {
        _inspection = inspection;
        _mes = mes;
    }

    /// <summary>
    /// 검사 실행. 지금은 사진 파일을 직접 받는다.
    /// (카메라를 붙이면 이미지 없이 productId만 받고 스테이션이 직접 찍게 바꾼다.)
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<InspectionResult>> Trigger(
        IFormFile image, [FromForm] string productId, [FromForm] string? workerId, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = new { code = "NO_IMAGE", message = "이미지가 없습니다." } });

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);

        var result = await _inspection.InspectAsync(ms.ToArray(), productId, ct);

        // MES로 보낼 기록은 보관함에 넣기만 한다 (MES가 꺼져 있어도 응답을 기다리게 하지 않음)
        _mes.Enqueue(result, productId, workerId);
        return Ok(result);
    }
}