"""브레드보드 핀홀 282개 전부를 구조화한 것.

각 핀홀마다 다음을 들고 있다:
- 라벨 (예: "J9")
- 이 이미지 기준 기대 픽셀 위치 (calibration.GridCalibration으로 계산)
- 부품에 의해 가려졌는지(=꽂혀 있는지) 여부, 가려졌다면 어떤 부품인지·신뢰도

흐름:
1. `all_holes(calibration)` 로 282개 핀홀 전부의 기대 픽셀 위치를 구한다.
2. YOLO가 반환한 부품 박스(DetectedBox, 픽셀 좌표 사각형)들을 `match_holes_to_boxes`로
   대조한다 -> 핀홀의 기대 픽셀 위치가 어떤 박스 안에 들어가면, 그 핀홀은
   해당 부품에 "가려짐"(장착됨) 으로 표시된다.

박스 중심 하나만 보고 핀홀 하나를 고르는 방식과 달리, 부품 하나가 핀홀
여러 개(예: 저항 양쪽 다리)에 걸치는 경우도 자연스럽게 처리된다.

판정(정상/누락/오장착 등)은 여기서 하지 않는다 — 그건 스테이션의 몫이고,
이 모듈은 "어느 핀홀이 가려져 있는지"까지만 구조화한다.
"""

from dataclasses import dataclass

from calibration import GridCalibration
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


def all_holes(calibration: GridCalibration) -> list[Hole]:
    """282개 핀홀 전부를, 이 이미지 기준 기대 픽셀 위치와 함께 만든다."""
    holes = []
    for row_i, row in enumerate(ROWS):
        col_min, col_max = col_range(row)
        for col_i in range(col_min, col_max + 1):
            pixel = calibration.grid_to_pixel(row_i, col_i)
            holes.append(Hole(label=f"{row}{col_i}", row=row_i, col=col_i, pixel=pixel))
    return holes


def match_holes_to_boxes(holes: list[Hole], boxes: list[DetectedBox]) -> None:
    """각 홀의 기대 픽셀 위치가 어떤 박스 안에 들어가는지 보고, 그 홀을 채운다.

    holes를 제자리에서 수정한다 (반환값 없음).
    """
    for hole in holes:
        x, y = hole.pixel
        for box in boxes:
            if box.contains(x, y):
                hole.part_class = box.part_class
                hole.confidence = box.confidence
                break  # 박스 두 개가 겹쳐서 같은 홀을 덮는 경우는 드묾 -> 먼저 찾은 것 사용
