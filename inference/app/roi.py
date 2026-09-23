"""고정 카메라·지그 세팅에서, 보드가 나오는 영역을 잘라낸다.

카메라와 촬영 지그가 고정되어 있어서 들어오는 이미지의 해상도·프레임은
항상 같다. 카메라가 눕혀서 찍기 때문에 먼저 왼쪽(반시계) 90도로 돌린다.

**90도 회전은 큰 방향만 맞출 뿐, 완벽한 수직을 보장하지 않는다** — 카메라
장착 각도가 정확히 90도가 아니면 레일 선이 아주 살짝 기울어져 있을 수
있다. 그래서 90도로 돌린 다음, 레일 선의 실제 기울기를 측정해서 그만큼
한 번 더 미세하게 돌린다(deskew, `rail_detect.measure_tilt_angle`). 이걸
먼저 한 뒤에 아래 두 크롭을 계산한다.

크롭 범위는 두 축 다 실측값으로 매번 다시 계산한다 (둘 다 고정 숫자로는
버티기 어렵다는 게 실측으로 확인됐다):

- 가로(열) 범위: **빨강/파랑 전원 레일 선을 색으로 찾아서 계산한다**
  ([rail_detect.py](rail_detect.py)). 실측해보니 사진마다 레일 선 x좌표가
  40px 넘게 달라졌다(카메라가 살짝씩 밀림). 프레임 안에 후보가 여러 개면
  (예: 테스트 사진처럼 보드가 여러 개 같이 찍힌 경우) **프레임 중앙에 가장
  가까운 것을 고른다** (`find_center_unit_x_range`) — 카메라·지그가 고정돼
  있으면 검사 대상 보드는 항상 프레임 중앙 부근에 놓인다는 가정. 레일 선
  검출 자체를 실패하면 예비 고정값(`FALLBACK_X_RANGE`)으로 대체한다.
- 세로(행) 범위: **처음엔 고정값(`Y_RANGE`)을 그대로 썼는데, 실측해보니
  사진마다 카메라 위치·줌이 미묘하게 달라서 같은 픽셀 범위가 행 A~U를
  못 다 담는 경우가 있었다** (`board2.jpg`에서 마지막 행이 잘림 — WORKLOG.md
  참고). 그래서 지금은 "10"·"30" 행 번호 라벨을 OCR로 읽어서
  ([label_detect.py](label_detect.py)) 그 사이 실제 간격으로 세로 범위를
  다시 계산한다. `Y_RANGE`는 이제 최종값이 아니라, 가로 레일 검출을 어느
  대역에서 할지 정하는 "대략 이 근처"용 시작점으로만 쓰인다. OCR이 라벨을
  못 찾으면 이 고정값을 그대로 세로 범위로 쓴다 (예비값 역할은 동일).

(홀 픽셀 위치가 더 미세하게 달라지는 부분은 이 크롭과는 별개로
board_detect.py가 크롭된 이미지 안에서 따로 계산한다. 이 모듈은 "보드가
있는 영역만 남기고 나머지는 잘라내는" 대략적인 역할까지만 한다.)
"""

import cv2
import numpy as np

import label_detect
import rail_detect

# 카메라가 눕혀서 찍혀서, 크롭보다 먼저 이 방향으로 돌려야 한다.
ROTATE = cv2.ROTATE_90_COUNTERCLOCKWISE

# 세로(행) 범위의 대략적인 시작점(고정값). 가로 레일 검출과, OCR 라벨 검색의
# 탐색 범위를 정하는 데 쓰인다. OCR이 라벨을 못 찾으면 이 값을 그대로 쓴다.
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

    # 미세 기울기 보정(deskew): 레일 선이 완전히 수직이 되도록 한 번 더 돌린다.
    # 회전하면 픽셀 위치가 바뀌므로, 가로 범위(x1,x2)를 다시 찾는다.
    try:
        angle = rail_detect.measure_tilt_angle(band, x1, x2)
        # cv2.getRotationMatrix2D는 양수 각도가 반시계 방향이라, 측정된
        # 기울기(시계 방향 양수)를 상쇄하려면 부호를 반대로 줘야 한다.
        rotated = _rotate_by_angle(rotated, -angle)
        band = rotated[y1:y2, :]
        x1, x2 = rail_detect.find_center_unit_x_range(band)
    except ValueError:
        pass  # 각도 측정·재검출 실패 시, 90도 회전 상태 그대로 진행

    try:
        y1, y2 = label_detect.refine_y_range(rotated, x1, x2, Y_RANGE)
    except label_detect.LabelsNotFoundError:
        pass  # 라벨을 못 찾으면 고정값(Y_RANGE) 그대로 사용

    return rotated[y1:y2, x1:x2]


def _rotate_by_angle(image: np.ndarray, angle_deg: float) -> np.ndarray:
    """이미지 중심 기준으로 angle_deg도 만큼 미세 회전한다 (크기는 그대로 유지)."""
    h, w = image.shape[:2]
    matrix = cv2.getRotationMatrix2D((w / 2, h / 2), angle_deg, 1.0)
    return cv2.warpAffine(image, matrix, (w, h), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
