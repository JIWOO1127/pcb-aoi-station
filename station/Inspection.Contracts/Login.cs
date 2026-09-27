namespace Inspection.Contracts;

/// <summary>HMI → 스테이션 로그인 요청 (사번 + PIN).</summary>
public record LoginRequest(string EmployeeId, string Pin);

/// <summary>로그인한 작업자. Offline=true면 MES가 꺼져 있어 HMI에 저장된 정보로 로그인한 것.</summary>
public record LoginResult(string EmployeeId, string Name, string Role, bool Offline);
