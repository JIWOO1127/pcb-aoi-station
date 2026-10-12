using Inspection.Contracts;
using Inspection.Station.Services.Mes;
using Microsoft.AspNetCore.Mvc;

namespace Inspection.Station.Controllers;

/// <summary>HMI가 MES 연결 상태를 묻는 곳 (헤더의 MES 점).</summary>
[ApiController]
[Route("api/mes")]
public class MesController : ControllerBase
{
    private readonly MesOutbox _outbox;

    public MesController(MesOutbox outbox) => _outbox = outbox;

    /// <summary>GET /api/mes/status → { enabled, connected, pending, lastSuccessAt, lastError }</summary>
    [HttpGet("status")]
    public ActionResult<MesStatus> Status() => Ok(_outbox.GetStatus());
}
