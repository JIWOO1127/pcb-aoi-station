"""핀홀 그리드 정의 및 좌표 -> 라벨 변환.

이 PCB는 실제 브레드보드가 아니라 브레드보드를 본뜬 커스텀 PCB이며,
행(row)은 A~U(21개, 위→아래), 열(column)은 0~13(14개, 왼쪽→오른쪽)이다.
(핀홀 라벨은 "행글자 + 열숫자" 순서, 예: "J9")

열 구조:
- 0, 1  : 왼쪽 전원 레일
- 2~6   : 왼쪽 단자부
- 7~11  : 오른쪽 단자부
- 12, 13: 오른쪽 전원 레일

E, K, Q 행은 전원 레일이 없어서 0, 1, 12, 13열 핀홀이 존재하지 않는다
(해당 행은 2~11열만 존재).

이 모듈은 "그리드 좌표(행 인덱스, 열 인덱스 실수값)"를 가장 가까운
실제 핀홀 라벨(예: "J9")로 스냅하는 것까지만 담당한다.

픽셀 좌표 <-> 그리드 좌표 변환은 [calibration.py](calibration.py),
핀홀 282개 전체를 이미지 속 위치·부품 점유 여부와 함께 구조화하는 것은
[board.py](board.py) 참고.
"""

ROWS = "ABCDEFGHIJKLMNOPQRSTU"  # 21개 행: A ~ U (위 -> 아래)
COL_MIN, COL_MAX = 0, 13  # 대부분 행의 열 범위 (왼쪽 -> 오른쪽)

# E, K, Q 행은 전원 레일(0, 1, 12, 13열)이 없다.
SHORT_ROWS = {"E", "K", "Q"}
SHORT_COL_MIN, SHORT_COL_MAX = 2, 11


def col_range(row: str) -> tuple[int, int]:
    """해당 행에 실제로 존재하는 열 범위 (최소, 최대). board.py에서도 사용."""
    if row in SHORT_ROWS:
        return SHORT_COL_MIN, SHORT_COL_MAX
    return COL_MIN, COL_MAX


def snap_to_pinhole(row_index: float, col_index: float) -> str:
    """그리드 좌표를 가장 가까운 실제 핀홀 라벨로 변환한다.

    row_index: 0 = 'A', 1 = 'B', ..., 20 = 'U' (범위 밖이면 가까운 끝으로 clamp)
    col_index: 0 ~ 13 (E/K/Q 행이면 2~11로 clamp)

    존재하지 않는 좌표(예: E0)로는 절대 스냅되지 않고, 항상 실제 핀홀 라벨을 반환한다.
    """
    row_i = round(row_index)
    row_i = min(max(row_i, 0), len(ROWS) - 1)
    row = ROWS[row_i]

    col_min, col_max = col_range(row)
    col_i = round(col_index)
    col_i = min(max(col_i, col_min), col_max)

    return f"{row}{col_i}"


def all_valid_pinholes() -> set[str]:
    """존재하는 모든 핀홀 라벨의 집합 (테스트·검증용)."""
    pins = set()
    for row in ROWS:
        col_min, col_max = col_range(row)
        for col in range(col_min, col_max + 1):
            pins.add(f"{row}{col}")
    return pins
