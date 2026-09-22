"""픽셀 좌표 <-> 그리드 좌표(행 인덱스, 열 인덱스) 변환.

보드의 "네 모서리 핀홀" — (행 0, 열 0) / (행 0, 열 13) / (행 20, 열 0) / (행 20, 열 13),
즉 A0, A13, U0, U13 — 의 픽셀 좌표만 알면, 원근 변환(호모그래피)으로 이미지 안
어떤 픽셀 좌표든 그리드 좌표(row_index, col_index)로 바꿀 수 있다.

촬영마다 보드가 이미지 안에서 조금씩 다르게 나와도(위치·기울기·크기가
미묘하게 달라도), 이 네 모서리 핀홀만 그때그때 다시 찾아서 넣어주면
나머지 홀 위치는 항상 같은 계산(호모그래피 적용)으로 구할 수 있다.
보드 전체가 항상 이미지 안에 다 들어온다는 전제(스테이션 확인됨)로 설계함.

아직 없는 것 (TODO):
- 원본 이미지에서 이 네 모서리 핀홀의 픽셀 좌표를 실제로 찾아내는 부분
  (예: find_corner_pinholes 같은 함수). 모서리에 별도 기준 마크가 있는지,
  아니면 핀홀 자체를 검출해서 가장자리 4개를 골라야 하는지는 실제 촬영
  이미지로 확인해야 함. 이 모듈은 "네 점을 이미 알고 있다고 할 때" 이후
  단계만 담당한다.

282개 핀홀 전부를 이 변환으로 구조화하는 것은 [board.py](board.py) 참고.
"""

from dataclasses import dataclass

import numpy as np

from pinhole_grid import COL_MAX, COL_MIN, ROWS

ROW_MIN_INDEX = 0
ROW_MAX_INDEX = len(ROWS) - 1  # 20 ('U')

Point = tuple[float, float]


@dataclass
class GridCalibration:
    """보드 네 모서리 핀홀의 픽셀 좌표로 만드는 픽셀 -> 그리드 변환기."""

    homography: np.ndarray  # 3x3. 픽셀(x, y, 1) -> 그리드(col, row, 1) 동차좌표

    @classmethod
    def from_corners(
        cls,
        top_left: Point,
        top_right: Point,
        bottom_left: Point,
        bottom_right: Point,
    ) -> "GridCalibration":
        """네 모서리 핀홀의 픽셀 좌표로 보정기를 만든다.

        top_left = A0, top_right = A13, bottom_left = U0, bottom_right = U13
        각각 (x_px, y_px)
        """
        src = np.array([top_left, top_right, bottom_left, bottom_right], dtype=np.float64)
        dst = np.array(
            [
                (COL_MIN, ROW_MIN_INDEX),
                (COL_MAX, ROW_MIN_INDEX),
                (COL_MIN, ROW_MAX_INDEX),
                (COL_MAX, ROW_MAX_INDEX),
            ],
            dtype=np.float64,
        )
        return cls(homography=_solve_homography(src, dst))

    def pixel_to_grid(self, x_px: float, y_px: float) -> tuple[float, float]:
        """픽셀 좌표 -> (row_index, col_index) 실수값.

        그대로 pinhole_grid.snap_to_pinhole(row_index, col_index)에 넣으면 된다.
        """
        vec = self.homography @ np.array([x_px, y_px, 1.0])
        col, row = vec[0] / vec[2], vec[1] / vec[2]
        return row, col

    def grid_to_pixel(self, row_index: float, col_index: float) -> tuple[float, float]:
        """그리드 좌표(행 인덱스, 열 인덱스) -> 픽셀 좌표.

        board.py가 282개 핀홀 전부의 "이 이미지 기준 기대 픽셀 위치"를
        미리 계산해둘 때 사용한다 (pixel_to_grid의 역변환).
        """
        inv = np.linalg.inv(self.homography)
        vec = inv @ np.array([col_index, row_index, 1.0])
        x, y = vec[0] / vec[2], vec[1] / vec[2]
        return x, y


def _solve_homography(src: np.ndarray, dst: np.ndarray) -> np.ndarray:
    """대응점 4쌍으로 3x3 호모그래피 행렬을 구한다 (OpenCV 없이 직접 계산)."""
    if len(src) != 4 or len(dst) != 4:
        raise ValueError("호모그래피 계산에는 대응점 4쌍이 필요합니다.")

    rows = []
    for (x, y), (u, v) in zip(src, dst):
        rows.append([x, y, 1, 0, 0, 0, -u * x, -u * y, -u])
        rows.append([0, 0, 0, x, y, 1, -v * x, -v * y, -v])
    a = np.array(rows, dtype=np.float64)

    _, _, vt = np.linalg.svd(a)
    h = vt[-1].reshape(3, 3)
    return h / h[2, 2]
