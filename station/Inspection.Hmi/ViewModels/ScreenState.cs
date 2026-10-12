namespace Inspection.Hmi.ViewModels;

/// <summary>
/// 작업자 화면의 상태. 판정 결과(Verdict)와 구분한다.
/// 판정은 "이 보드가 양품인가"이고, 화면 상태는 "지금 작업자가 무엇을 해야 하는가"다.
/// 예: FAIL을 불량 확정하면 판정은 그대로지만 화면은 Idle로 돌아간다.
/// </summary>
public enum ScreenState
{
    Idle,       // 보드를 올리고 검사를 기다림
    Busy,       // 검사 중
    Pass,       // 양품
    Fail,       // 불량 — 수리 후 재검사하거나 불량 확정
    Invalid,    // 판정 불가 — 재검사
    Error       // 설비·연결 이상
}
