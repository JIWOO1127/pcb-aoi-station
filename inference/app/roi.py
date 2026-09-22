"""고정 카메라·지그 세팅에서, 보드가 나오는 영역을 잘라낸다.

카메라와 촬영 지그가 고정되어 있어서 들어오는 이미지의 해상도·프레임은
항상 같다. 카메라가 눕혀서 찍기 때문에 먼저 왼쪽(반시계) 90도로 돌린다.

크롭 범위는 두 축을 다르게 다룬다:
- 세로(행) 범위 `Y_RANGE`: 고정값. (보드가 위아래로는 잘 안 움직인다고 보고 고정)
- 가로(열) 범위: **고정하지 않고, 매번 빨강/파랑 레일 선을 색으로 찾아서 계산한다**
  ([rail_detect.py](rail_detect.py)). 실측해보니 사진마다 레일 선 x좌표가
  40px 넘게 달라지는 걸 확인했어서(카메라가 살짝씩 밀림), 가로는 고정 숫자로
  버티기 어렵다고 판단함. 프레임 안에 후보가 여러 개면(예: 테스트 사진처럼
  보드가 여러 개 같이 찍힌 경우) **프레임 중앙에 가장 가까운 것을 고른다**
  (`find_center_unit_x_range`). 카메라·지그가 고정돼 있으면 검사 대상 보드는
  항상 프레임 중앙 부근에 놓이므로, "부품이 많아 보이는 곳"(edge 밀도)보다
  "위치가 중앙인 곳"이 더 안정적인 기준이다 — 부품 개수나 조명에 흔들리지
  않는다. 실제 검사 라인처럼 보드가 하나뿐이면 후보도 하나뿐이라 결과는 똑같다.
  레일 선 검출 자체를 실패하면 예비 고정값(FALLBACK_X_RANGE)으로 대체한다.

(홀 픽셀 위치가 더 미세하게 달라지는 부분은 이 크롭과는 별개로 calibration.py가
크롭된 이미지 안에서 따로 보정한다. 이 모듈은 "보드가 있는 영역만 남기고
나머지는 잘라내는" 대략적인 역할까지만 한다.)
"""

import cv2
import numpy as np

import rail_detect

# 카메라가 눕혀서 찍혀서, 크롭보다 먼저 이 방향으로 돌려야 한다.
ROTATE = cv2.ROTATE_90_COUNTERCLOCKWISE

# 세로(행) 범위는 고정값. 회전 후 기준.
Y_RANGE: tuple[int, int] = (646, 1818)

# 레일 선 검출이 실패했을 때 쓸 예비 가로 범위 (실측 사진 기준 고정값).
FALLBACK_X_RANGE: tuple[int, int] = (1350, 2630)


def crop_to_roi(image: np.ndarray) -> np.ndarray:
    """디코딩된 원본 이미지를 회전한 뒤, 보드가 있는 영역으로 잘라낸다.

    image: cv2로 디코딩한 BGR 배열 (H, W, 3), 회전 전(카메라가 찍은 그대로)
    """
    rotated = cv2.rotate(image, ROTATE)
    y1, y2 = Y_RANGE
    band = rotated[y1:y2, :]

    try:
        x1, x2 = rail_detect.find_center_unit_x_range(band)
    except ValueError:
        x1, x2 = FALLBACK_X_RANGE

    return band[:, x1:x2]
