"""개발용 디버그 스크립트: 크롭된 보드 이미지에 board_detect의 핀홀 검출 결과를
그려서 눈으로 확인한다. 실제 서비스 코드가 아니라 시각 확인용.

사용법: python dev_plot_pinholes.py <입력 크롭 이미지> <출력 이미지>
"""

import sys
from pathlib import Path

import cv2
import numpy as np

import pinhole_grid
from board_detect import find_pinhole_pixels

_CORNER_LABELS = {"A0", "A13", "U0", "U13"}


def load(path: str) -> np.ndarray:
    return cv2.imdecode(np.frombuffer(Path(path).read_bytes(), np.uint8), cv2.IMREAD_COLOR)


def save(path: str, img: np.ndarray) -> None:
    ok, buf = cv2.imencode(".jpg", img)
    Path(path).write_bytes(buf.tobytes())


def draw_pinholes(image: np.ndarray, pixel_by_label: dict[str, tuple[float, float]]) -> np.ndarray:
    vis = image.copy()
    for row_letter in pinhole_grid.ROWS:
        col_min, col_max = pinhole_grid.col_range(row_letter)
        for col in range(col_min, col_max + 1):
            label = f"{row_letter}{col}"
            x, y = pixel_by_label[label]
            x, y = int(round(x)), int(round(y))
            if label in _CORNER_LABELS:
                cv2.circle(vis, (x, y), 10, (0, 0, 255), 2)
                cv2.putText(vis, label, (x - 15, y - 15), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 2)
            else:
                cv2.circle(vis, (x, y), 3, (0, 255, 0), -1)
    return vis


def main():
    if len(sys.argv) != 3:
        print(f"usage: {sys.argv[0]} <input> <output>")
        sys.exit(1)

    image = load(sys.argv[1])
    detected = find_pinhole_pixels(image)
    vis = draw_pinholes(detected.image, detected.pixel_by_label)
    save(sys.argv[2], vis)
    print(f"저장됨: {sys.argv[2]}")


if __name__ == "__main__":
    main()
