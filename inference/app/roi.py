"""고정 카메라·지그 세팅에서, 보드가 나오는 영역을 잘라낸다.

카메라·지그가 고정이긴 해도, 카메라 기종·마운트 방향이 바뀌면 사진이 어느
방향으로 눕혀서 찍힐지도 달라질 수 있다 (실측: 기존 4032×3024 사진은
반시계 90도가 정답이었는데, 새 720×720 사진은 방향 자체가 또 달랐다 —
WORKLOG.md 참고). 그래서 "몇 도 돌려야 한다"를 코드에 고정해두는 대신,
**0/90/180/270도 네 방향을 다 시도**해서 레일(빨강/파랑)도 찾아지고 행 번호
라벨("10"~"30")도 OCR로 읽히는 방향을 정답으로 채택한다(`_find_orientation`).
OCR이 되는지 여부 자체가 "방향이 맞았는지"의 검증 수단이기도 하다 —
방향이 틀리면 숫자가 거꾸로/옆으로 찍혀서 Tesseract가 거의 못 읽는다.

방향을 정하고 나면, 레일 선의 실제 기울기를 재서 완전한 수직(0도)이
되도록 한 번 더 미세하게 돌린다(deskew, `rail_detect.measure_tilt_angle`)
— 카메라 장착 각도가 정확히 직각이 아니면 큰 방향만 맞춘 것으로는 여전히
살짝 기울어져 있을 수 있어서다.

크롭 범위는 두 축 다 실측값으로 매번 계산한다 (고정 픽셀 숫자로는 사진마다
버티기 어렵다는 게 실측으로 확인됐다):

- 가로(열) 범위: **빨강/파랑 전원 레일 선을 색으로 찾아서 계산한다**
  ([rail_detect.py](rail_detect.py)). 프레임 안에 후보가 여러 개면(예:
  단위 여러 개가 같이 찍힌 사진) **프레임 중앙에 가장 가까운 것을 고른다**
  (`find_center_unit_x_range`) — 카메라·지그가 고정돼 있으면 검사 대상
  보드는 항상 프레임 중앙 부근에 놓인다는 가정. 레일 선 검출 자체를
  실패하면 예비 고정값(`FALLBACK_X_RANGE`)으로 대체한다.
- 세로(행) 범위: "10"·"30" 행 번호 라벨을 OCR로 읽어서
  ([label_detect.py](label_detect.py)) 그 사이 실제 간격으로 계산한다.
  OCR이 라벨을 못 찾으면 예비 고정값(`Y_RANGE`)으로 대체한다.

(홀 픽셀 위치가 더 미세하게 달라지는 부분은 이 크롭과는 별개로
board_detect.py가 크롭된 이미지 안에서 따로 계산한다. 이 모듈은 "보드가
있는 영역만 남기고 나머지는 잘라내는" 대략적인 역할까지만 한다.)
"""

import cv2
import numpy as np

import label_detect
import rail_detect
import scale

# 방향 탐색·라벨 검출 자체가 실패했을 때만 쓰는 최후의 예비값들
# (기존 4032×3024 사진 실측 기준 고정값).
ROTATE = cv2.ROTATE_90_COUNTERCLOCKWISE
Y_RANGE: tuple[int, int] = (646, 1818)
FALLBACK_X_RANGE: tuple[int, int] = (1350, 2630)

# 방향 탐색에서 시도해볼 정방향 회전 네 가지 (None = 회전 없음).
_CARDINAL_ROTATIONS = (None, cv2.ROTATE_90_CLOCKWISE, cv2.ROTATE_180, cv2.ROTATE_90_COUNTERCLOCKWISE)


def crop_to_roi(image: np.ndarray) -> tuple[np.ndarray, float, list[tuple[int, float, float]]]:
    """디코딩된 원본 이미지를 방향 맞춰 돌린 뒤, 보드가 있는 영역으로 잘라낸다.

    image: cv2로 디코딩한 BGR 배열 (H, W, 3), 회전 전(카메라가 찍은 그대로)

    반환: (크롭된 이미지, 배율, 라벨 앵커 목록).

    배율은 board_detect 등에서 픽셀 기반 상수를 조정하는 데 쓰는데,
    **해상도 비율이 아니라 이 사진에서 OCR로 실측한 행 피치(간격)를 기준
    피치와 비교한 값**이다 — 카메라가 해상도는 낮아도 훨씬 가까이서
    찍으면(줌인) 홀 간격이 오히려 기준 사진과 비슷하거나 더 클 수 있어서,
    해상도만으로 배율을 추정하면 틀린다 (실측: 720×720 사진에서 해상도
    기준 배율은 0.2였지만 실제 행 피치는 기준과 거의 같았음 — WORKLOG.md
    참고). 라벨을 아예 못 찾은 경우에만 해상도 기준 배율로 대체한다.

    라벨 앵커 목록은 [(행번호, x, y), ...] — OCR로 찾은 "10"~"30" 라벨
    위치를 **크롭 후 좌표계**로 옮긴 것. board_detect가 행 평면을 피팅할 때
    "이 열의 마지막 점 = U행"이라는 열별 추정 대신 쓸 수 있는, 훨씬 믿을
    만한 앵커다 (WORKLOG.md의 "라벨 기반 행 앵커" 참고). 라벨을 못 찾으면
    빈 리스트.
    """
    img_scale = scale.compute_scale(image.shape[:2])
    label_points: list[tuple[int, float, float]] = []

    try:
        rotated, x1, x2 = _find_orientation(image, img_scale)
    except ValueError:
        # 방향 탐색이 통째로 실패하면, 예전 고정 방식(반시계 90도 + 고정
        # 범위)으로 대체한다.
        rotated = cv2.rotate(image, ROTATE)
        y1 = round(Y_RANGE[0] * img_scale)
        y2 = round(Y_RANGE[1] * img_scale)
        try:
            x1, x2 = rail_detect.find_center_unit_x_range(rotated[y1:y2, :], img_scale)
        except ValueError:
            x1 = round(FALLBACK_X_RANGE[0] * img_scale)
            x2 = round(FALLBACK_X_RANGE[1] * img_scale)

    # 미세 기울기 보정(deskew): 레일 선이 완전히 수직이 되도록 한 번 더 돌린다.
    # 회전하면 픽셀 위치가 바뀌므로, 가로 범위(x1,x2)를 다시 찾는다.
    try:
        angle = rail_detect.measure_tilt_angle(rotated, x1, x2, img_scale)
        # cv2.getRotationMatrix2D는 양수 각도가 반시계 방향이라, 측정된
        # 기울기(시계 방향 양수)를 상쇄하려면 부호를 반대로 줘야 한다.
        rotated = _rotate_by_angle(rotated, -angle)
        x1, x2 = rail_detect.find_center_unit_x_range(rotated, img_scale)
    except ValueError:
        pass  # 각도 측정·재검출 실패 시, 방향만 맞춘 상태로 진행

    try:
        y1, y2, pitch, label_points = label_detect.refine_y_range(rotated, x1, x2, (0, rotated.shape[0]), img_scale)
        hole_scale = pitch / label_detect._DEFAULT_PITCH
    except label_detect.LabelsNotFoundError:
        y1 = round(Y_RANGE[0] * img_scale)
        y2 = round(Y_RANGE[1] * img_scale)
        hole_scale = img_scale  # 라벨을 못 찾으면 실측 피치가 없으니 해상도 배율로 대체

    # 라벨 앵커를 크롭 후 좌표계로 옮긴다 (rotated 좌표 -> 크롭 좌표: x1,y1만큼 빼기).
    crop_anchors = [(row_index, x - x1, y - y1) for row_index, x, y in label_points]

    return rotated[y1:y2, x1:x2], hole_scale, crop_anchors


def _find_orientation(image: np.ndarray, img_scale: float) -> tuple[np.ndarray, int, int]:
    """0/90/180/270도 중, 레일도 찾아지고 라벨(OCR)도 읽히는 방향을 찾는다.

    반환: (그 방향으로 돌린 이미지, x1, x2). 넷 다 실패하면 ValueError.
    """
    for rotation in _CARDINAL_ROTATIONS:
        candidate = image if rotation is None else cv2.rotate(image, rotation)
        try:
            x1, x2 = rail_detect.find_center_unit_x_range(candidate, img_scale)
        except ValueError:
            continue
        try:
            label_detect.find_label_row_points(candidate, x1, x2, (0, candidate.shape[0]), img_scale)
        except label_detect.LabelsNotFoundError:
            continue  # 레일은 찾았지만 라벨이 안 읽히면 방향이 틀렸을 가능성이 높음
        return candidate, x1, x2
    raise ValueError("네 방향 모두 레일/라벨을 찾지 못했습니다.")


def _rotate_by_angle(image: np.ndarray, angle_deg: float) -> np.ndarray:
    """이미지 중심 기준으로 angle_deg도 만큼 미세 회전한다 (크기는 그대로 유지)."""
    h, w = image.shape[:2]
    matrix = cv2.getRotationMatrix2D((w / 2, h / 2), angle_deg, 1.0)
    return cv2.warpAffine(image, matrix, (w, h), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
