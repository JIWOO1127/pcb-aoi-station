"""이미지 해상도에 따라 픽셀 기반 상수들을 비례 조정하기 위한 배율 계산.

`roi.py`, `rail_detect.py`, `label_detect.py`, `board_detect.py`에 있는 여러
상수(예: `Y_RANGE`, `_CLUSTER_GAP_PX`, `_MIN_BLOB_AREA`...)는 전부 "원본 사진이
대략 4032×3024(회전 후 3024×4032)"라는 가정 하에 실측으로 튜닝된 값이다.
카메라가 그보다 낮은 해상도(예: 1280×720)면, 홀 간격·글자 크기 등 실제 물리적
거리가 이미지 안에서 훨씬 적은 픽셀로 표현되는데 기준값은 그대로라서, 크롭
범위가 이미지 밖으로 나가거나(빈 이미지) 열 검출이 깨지는 문제가 있었다
(WORKLOG.md 참고).

해결: 실제 이미지 크기를 기준 해상도와 비교한 "배율" 하나를 구해서, 픽셀
상수에 이 배율을 곱해 쓴다. 카메라·지그가 고정이라 해상도만 다르면 같은
장면이 비례해서 더 적은/많은 픽셀로 찍힌다는 가정이다 (가로세로 비율까지
다른 카메라라면 완벽하진 않지만, 크래시 나거나 완전히 엉뚱한 범위를 잡는
것보다는 훨씬 낫다).
"""

import math

# 기존 상수들을 튜닝할 때 쓴 기준 해상도 (회전 후 기준: 세로 3024 x 가로 4032).
REFERENCE_HEIGHT = 3024
REFERENCE_WIDTH = 4032


def compute_scale(shape: tuple[int, int]) -> float:
    """image.shape[:2] 같은 (height, width)를 받아 기준 해상도 대비 배율을 구한다.

    면적비의 제곱근을 쓴다 — 가로/세로 중 하나만으로 배율을 구하면 가로세로
    비율이 기준과 다른 카메라(예: 16:9 vs 기준 4:3)에서 한쪽으로 치우치는데,
    면적비 제곱근은 그 사이 어딘가로 무난하게 절충된다.
    """
    height, width = shape[0], shape[1]
    return math.sqrt((height * width) / (REFERENCE_HEIGHT * REFERENCE_WIDTH))
