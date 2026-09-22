using System;
using System.Collections.Generic;
using System.Text;
using Inspection.Contracts;

namespace Inspection.Hmi.Services;

/// <summary>스테이션 서버와 통신하는 약속. 화면은 이 인터페이스만 안다.</summary>
public interface IStationClient
{
    Task<InspectionResult> TriggerAsync(string productId, CancellationToken ct = default);
}