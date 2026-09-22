"""빨강/파랑 전원 레일 선을 색으로 찾아서, 크롭할 x축 범위를 그때그때 계산한다.

roi.py의 고정 ROI(하드코딩된 픽셀 숫자)와 달리, 이미지 안의 실제 레일 선을
색으로 검출해서 크롭 범위를 다시 계산한다. 카메라가 살짝 움직이거나
좌우로 몇십 픽셀 밀려도 선을 다시 찾아서 따라가므로, 고정 숫자보다 강건하다.

**이건 머신비전(색상 기반 규칙 검출)이지 딥러닝이 아니다** — 신경망 없이
HSV 색공간에서 빨강/파랑 범위를 임계값으로 자르는 전통적인 이미지 처리 기법.
(WORKLOG.md의 "머신비전 구성 개념 정리" 참고)

레일 구조: 한 "단위"는 [빨강][레일홀 2열][파랑][단자부 5열][gap][단자부 5열]
[빨강][레일홀 2열][파랑] 순서로 반복된다. 즉 레일 하나는 항상 빨강이 왼쪽,
파랑이 오른쪽이다.
"""

import cv2
import numpy as np

# HSV 임계값 (OpenCV hue 범위 0~179). 실측(board1.jpg)으로 잡은 값.
_RED_HUE_LOW = 8
_RED_HUE_HIGH = 172
_BLUE_HUE_LOW = 100
_BLUE_HUE_HIGH = 115
_MIN_SATURATION = 100
_MIN_VALUE = 80

# 선으로 인정하려면 세로로 이 비율 이상 이어져 있어야 한다 (잡음 제거).
_MIN_LINE_HEIGHT_RATIO = 0.5
# 몇 픽셀 이내면 같은 선(굵기)으로 묶을지.
_CLUSTER_GAP_PX = 5


def _cluster_columns(mask: np.ndarray) -> list[int]:
    """마스크(불리언 배열)에서, 세로로 충분히 이어진 열들을 찾아 선의 x좌표로 묶는다."""
    height = mask.shape[0]
    col_counts = mask.sum(axis=0)
    xs = np.where(col_counts > height * _MIN_LINE_HEIGHT_RATIO)[0]
    if len(xs) == 0:
        return []

    groups: list[list[int]] = [[int(xs[0])]]
    for x in xs[1:]:
        if x - groups[-1][-1] <= _CLUSTER_GAP_PX:
            groups[-1].append(int(x))
        else:
            groups.append([int(x)])
    return [int(np.mean(g)) for g in groups]


def find_rail_lines(image: np.ndarray) -> tuple[list[int], list[int]]:
    """이미지에서 빨강/파랑 레일 선의 x좌표들을 찾는다 (왼쪽 -> 오른쪽 순).

    image: 세로로 자를 구간(대략적인 행 범위)만 담은 배열을 넣는 게 좋다.
    보드 여러 개가 위아래로 겹쳐 있으면, 다른 보드의 레일까지 섞여서
    잡힐 수 있기 때문이다.
    """
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    h, s, v = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]

    red_mask = ((h < _RED_HUE_LOW) | (h > _RED_HUE_HIGH)) & (s > _MIN_SATURATION) & (v > _MIN_VALUE)
    blue_mask = (h > _BLUE_HUE_LOW) & (h < _BLUE_HUE_HIGH) & (s > _MIN_SATURATION) & (v > _MIN_VALUE)

    return _cluster_columns(red_mask), _cluster_columns(blue_mask)


def find_unit_x_range(image: np.ndarray) -> tuple[int, int]:
    """"레일-단자부-단자부-레일" 한 단위의 x범위(x1, x2)를 찾는다.

    x1 = 이 단위를 여는 빨강선, x2 = 이 단위를 닫는(두 번째) 파랑선.
    보드 한 장만 프레임에 있는 정상 상황이면 빨강 2개·파랑 2개가 나오고,
    x1=red[0], x2=blue[1]이 곧 보드 전체 폭이 된다.

    프레임에 보드(또는 단위)가 여러 개 있으면 무조건 제일 왼쪽 것을 고른다.
    "부품이 있는 쪽"을 고르고 싶으면 find_populated_unit_x_range를 쓴다.
    """
    reds, blues = find_rail_lines(image)
    if len(reds) < 1 or len(blues) < 2:
        raise ValueError(
            f"레일 선을 충분히 찾지 못했습니다 (빨강={reds}, 파랑={blues}). "
            "조명·각도를 확인하거나 임계값을 조정하세요."
        )
    return reds[0], blues[1]


def _all_unit_candidates(image: np.ndarray) -> list[tuple[int, int]]:
    """검출된 레일 선들로 만들 수 있는 모든 "한 단위" 후보 (x1, x2) 목록."""
    reds, blues = find_rail_lines(image)
    n = min(len(reds), len(blues) - 1)
    if n < 1:
        raise ValueError(
            f"레일 선을 충분히 찾지 못했습니다 (빨강={reds}, 파랑={blues})."
        )
    return [(reds[i], blues[i + 1]) for i in range(n)]


def _edge_density(image: np.ndarray, x1: int, x2: int) -> float:
    """구간 (x1, x2) 안의 Canny edge 픽셀 비율. 부품이 많을수록 값이 커진다."""
    region = image[:, x1:x2]
    gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY)
    return float(cv2.Canny(gray, 50, 150).mean())


def find_populated_unit_x_range(image: np.ndarray) -> tuple[int, int]:
    """레일 선으로 찾은 후보 단위들 중, 부품이 있을 가능성이 가장 높은 곳을 고른다.

    프레임 안에 보드(단위)가 여러 개 있을 때(예: 책상에 여러 개 늘어놓고
    찍은 사진) "부품이 실제로 꽂혀 있는 구간"을 자동으로 골라내기 위한 것.
    빈 브레드보드는 흰 배경에 규칙적인 구멍만 있어 밋밋하지만, 부품이 있으면
    색·형태가 다양해져서 Canny edge가 뚜렷하게 늘어난다는 점을 이용한다.

    **딥러닝이 아니라 edge 밀도 기반 전통적 머신비전.**
    실제 검사 라인(보드 하나만 지그에 고정)에서는 후보가 하나뿐이라
    find_unit_x_range와 결과가 같다 — 즉 상위 호환이라 이 함수를 기본으로 써도 된다.
    """
    candidates = _all_unit_candidates(image)
    return max(candidates, key=lambda xy: _edge_density(image, *xy))


def find_center_unit_x_range(image: np.ndarray) -> tuple[int, int]:
    """레일 선으로 찾은 후보 단위들 중, 프레임 중앙에 가장 가까운 것을 고른다.

    실제 검사 라인은 카메라·지그가 고정되어 있어서, 검사 대상 보드는 항상
    프레임 안의 거의 같은 위치(대략 중앙 부근)에 놓인다고 볼 수 있다. 이
    가정이 맞다면 "부품이 많아 보이는 곳"(edge 밀도, find_populated_unit_x_range)
    보다 "프레임 중앙에 가장 가까운 곳"이 더 안정적인 기준이다 — 부품이 몇 개
    꽂혀 있는지나 조명에 흔들리지 않고, 오직 지그가 보드를 놓는 물리적 위치에만
    의존하기 때문이다.

    테스트 사진처럼 책상에 보드를 여러 개 늘어놓고 찍은 경우에도, "실제 검사
    위치에 해당하는 가운데 보드"를 고르는 데 이 기준을 쓴다.
    실제 검사 라인(보드 하나만 지그에 고정)에서는 후보가 하나뿐이라
    find_unit_x_range와 결과가 같다.
    """
    candidates = _all_unit_candidates(image)
    frame_center = image.shape[1] / 2

    def _distance_to_center(xy: tuple[int, int]) -> float:
        x1, x2 = xy
        return abs((x1 + x2) / 2 - frame_center)

    return min(candidates, key=_distance_to_center)
