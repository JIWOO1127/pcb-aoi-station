"""브레드보드 핀홀 282개 전부를 구조화한 것.

각 핀홀마다 다음을 들고 있다:
- 라벨 (예: "J9")
- 이 이미지 기준 기대 픽셀 위치 (board_detect.find_pinhole_pixels로 계산)
- 부품에 의해 가려졌는지(=꽂혀 있는지) 여부, 가려졌다면 어떤 부품인지·신뢰도

흐름:
1. `all_holes_from_image(image)` 로 282개 핀홀 전부의 기대 픽셀 위치를 구한다
   (내부적으로 board_detect가 이미지 안의 홀을 열마다 직접 검출한다. 이미지
   자체는 변형하지 않는다 — board_detect.py 문서 참고).
2. YOLO가 반환한 부품 박스(DetectedBox, 픽셀 좌표 사각형)들을 `match_holes_to_boxes`로
   대조한다 -> 핀홀의 기대 픽셀 위치가 어떤 박스 안에 들어가면, 그 핀홀은
   해당 부품에 "가려짐"(장착됨) 으로 표시된다.

박스 중심 하나만 보고 핀홀 하나를 고르는 방식과 달리, 부품 하나가 핀홀
여러 개(예: 저항 양쪽 다리)에 걸치는 경우도 자연스럽게 처리된다.

판정(정상/누락/오장착 등)은 여기서 하지 않는다 — 그건 스테이션의 몫이고,
이 모듈은 "어느 핀홀이 가려져 있는지"까지만 구조화한다.

(예전에는 네 모서리 호모그래피로 위치를 계산했는데, 열 간격이 균등하지
않은 걸 표현 못 해서 부정확했다 — 지금은 삭제됨. 자세한 시행착오는
WORKLOG.md 참고.)
"""

from dataclasses import dataclass

import numpy as np

from board_detect import find_pinhole_pixels
from pinhole_grid import ROWS, col_range


@dataclass
class DetectedBox:
    """YOLO가 반환한 부품 박스 (픽셀 좌표, 정렬된 이미지 기준)."""

    x1: float
    y1: float
    x2: float
    y2: float
    part_class: str
    confidence: float

    def contains(self, x: float, y: float) -> bool:
        return self.x1 <= x <= self.x2 and self.y1 <= y <= self.y2


@dataclass
class Hole:
    label: str  # 예: "J9"
    row: int  # 0~20 ('A'~'U')
    col: int  # 0~13
    pixel: tuple[float, float]  # 이 이미지 기준 기대 픽셀 위치
    part_class: str | None = None
    confidence: float | None = None

    @property
    def covered(self) -> bool:
        return self.part_class is not None


def all_holes_from_image(
    image: np.ndarray, scale: float = 1.0, label_anchors: list[tuple[int, float, float]] | None = None
) -> tuple[np.ndarray, list[Hole]]:
    """이미지에서 홀을 직접 검출해 282개 핀홀을 만든다.

    board_detect.find_pinhole_pixels로 열마다 실제 열 간격·기울기를 실측한다.
    이미지 자체는 변형하지 않는다.

    scale: `roi.crop_to_roi`가 반환한 해상도 배율. board_detect의 픽셀 기반
    상수들을 실제 해상도에 맞게 비례 조정하는 데 쓰인다 (scale.py 참고).

    label_anchors: `roi.crop_to_roi`가 반환한, OCR로 찾은 행 번호 라벨의
    (행번호, x, y) 앵커 목록(크롭 후 좌표계). 있으면 board_detect가 행 평면을
    피팅할 때 열별 추정보다 이 앵커를 우선한다 (WORKLOG.md 참고).

    반환: (입력 image 그대로, Hole 리스트). image도 함께 반환하는 건 나중에
    실제로 이미지를 보정하는 방식으로 바뀌어도 호출부(main.py)가 안 바뀌게 하기 위함.
    """
    detected = find_pinhole_pixels(image, scale, label_anchors)
    holes = []
    for row_i, row in enumerate(ROWS):
        col_min, col_max = col_range(row)
        for col_i in range(col_min, col_max + 1):
            label = f"{row}{col_i}"
            holes.append(Hole(label=label, row=row_i, col=col_i, pixel=detected.pixel_by_label[label]))
    return detected.image, holes


# LED·커패시터는 둥근 몸통이 여러 핀홀 위에 걸쳐 보이지만(박스 안에 핀홀
# 후보가 여러 개 잡힘), 실제 다리는 2개뿐이고 몸통 중심 바로 아래로 나온다.
# 그래서 박스 안 후보 중 "박스 중심에 가장 가까운 2개"를 진짜 다리로 본다.
_LEG_COUNT_BY_NEAREST_CENTER = {
    "led": 2,
    "capacitor": 2,
}

# IC칩은 다리가 정확히 14개인데, 몸통 양옆(가장 바깥쪽 두 열)에만 있다 —
# 중간 열은 몸통에 가려져 보일 뿐 실제 다리가 없다. 그래서 후보를 바깥쪽
# 두 열로 먼저 좁힌 다음, 그중 박스 중심에서 가장 가까운 14개만 다리로 본다.
_IC_CHIP_CLASS = "ic_chip"
_IC_CHIP_LEG_COUNT = 14

# 전선·저항은 길쭉한 몸통 양 끝에만 다리가 있다(중심 근처엔 다리가 없음).
# 그래서 박스의 긴 축(가로/세로 중 더 긴 쪽) 방향으로 정렬해서 양 끝 2개만
# 다리로 본다.
_ELONGATED_PART_CLASSES = {"black_wire", "red_wire", "white_wire", "resistance330", "resistance470"}
_ELONGATED_PART_LEG_COUNT = 2


def match_holes_to_boxes(holes: list[Hole], boxes: list[DetectedBox]) -> None:
    """각 홀의 기대 픽셀 위치가 어떤 박스 안에 들어가는지 보고, 그 홀을 채운다.

    holes를 제자리에서 수정한다 (반환값 없음).
    """
    for box in boxes:
        contained = [hole for hole in holes if hole.part_class is None and box.contains(*hole.pixel)]
        if not contained:
            continue

        if box.part_class == _IC_CHIP_CLASS:
            min_col = min(hole.col for hole in contained)
            max_col = max(hole.col for hole in contained)
            contained = [hole for hole in contained if hole.col in (min_col, max_col)]
            cx, cy = (box.x1 + box.x2) / 2, (box.y1 + box.y2) / 2
            contained.sort(key=lambda hole: (hole.pixel[0] - cx) ** 2 + (hole.pixel[1] - cy) ** 2)
            contained = contained[:_IC_CHIP_LEG_COUNT]
        elif box.part_class in _LEG_COUNT_BY_NEAREST_CENTER:
            cx, cy = (box.x1 + box.x2) / 2, (box.y1 + box.y2) / 2
            contained.sort(key=lambda hole: (hole.pixel[0] - cx) ** 2 + (hole.pixel[1] - cy) ** 2)
            contained = contained[:_LEG_COUNT_BY_NEAREST_CENTER[box.part_class]]
        elif box.part_class in _ELONGATED_PART_CLASSES and len(contained) > _ELONGATED_PART_LEG_COUNT:
            horizontal = (box.x2 - box.x1) >= (box.y2 - box.y1)
            axis_key = (lambda hole: hole.pixel[0]) if horizontal else (lambda hole: hole.pixel[1])
            contained.sort(key=axis_key)
            contained = [contained[0], contained[-1]]

        for hole in contained:
            hole.part_class = box.part_class
            hole.confidence = box.confidence
            # 박스 두 개가 겹쳐서 같은 홀을 덮는 경우는 드묾 -> 먼저 찾은 박스 사용
            # (hole.part_class is None 조건으로 이미 걸러짐)
