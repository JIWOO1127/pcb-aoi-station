"""개발용 디버그 스크립트: 원본(자르기 전) 사진 한 장을 넣으면, 실제 서비스와 똑같이
크롭 -> 핀홀 위치 계산 -> YOLO 부품 탐지 -> 홀/박스 대조까지 돌린 뒤, 결과를 한 장의
이미지로 그려서 눈으로 확인한다. 실제 서비스 코드가 아니라 시각 확인용.

- 초록 점: 검출된 핀홀 (비어있음)
- 노란 점: 부품에 가려진(=장착된) 핀홀
- 빨강 원+라벨: 네 모서리(A0/A13/U0/U13)
- 박스: YOLO가 탐지한 부품 (클래스명 + 신뢰도), 부품 종류별로 색이 다름

사용법: python dev_plot_infer.py <원본 이미지> <출력 이미지>
"""

import sys
from pathlib import Path

import cv2
import numpy as np

import pinhole_grid
from board import all_holes_from_image, match_holes_to_boxes
from main import detect_parts
from roi import crop_to_roi

_CORNER_LABELS = {"A0", "A13", "U0", "U13"}

# 부품 클래스별 박스 색(BGR). yolo8n.pt가 학습한 8종 기준.
_CLASS_COLORS = {
    "black_wire": (60, 60, 60),
    "red_wire": (0, 0, 255),
    "white_wire": (220, 220, 220),
    "ic_chip": (255, 140, 0),
    "led": (0, 255, 255),
    "capacitor": (255, 0, 255),
    "resistance330": (0, 200, 0),
    "resistance470": (0, 128, 255),
}
_DEFAULT_COLOR = (0, 140, 255)  # 목록에 없는 클래스면 기존 주황색으로


def _color_for(part_class: str) -> tuple[int, int, int]:
    return _CLASS_COLORS.get(part_class, _DEFAULT_COLOR)


def load(path: str) -> np.ndarray:
    return cv2.imdecode(np.frombuffer(Path(path).read_bytes(), np.uint8), cv2.IMREAD_COLOR)


def save(path: str, img: np.ndarray) -> None:
    ok, buf = cv2.imencode(".jpg", img)
    Path(path).write_bytes(buf.tobytes())


def draw(image: np.ndarray, holes, boxes) -> np.ndarray:
    vis = image.copy()

    for box in boxes:
        pt1 = (int(round(box.x1)), int(round(box.y1)))
        pt2 = (int(round(box.x2)), int(round(box.y2)))
        color = _color_for(box.part_class)
        cv2.rectangle(vis, pt1, pt2, color, 2)
        label = f"{box.part_class} {box.confidence:.2f}"
        cv2.putText(vis, label, (pt1[0], max(0, pt1[1] - 6)), cv2.FONT_HERSHEY_SIMPLEX, 0.5, color, 1)

    for hole in holes:
        x, y = int(round(hole.pixel[0])), int(round(hole.pixel[1]))
        if hole.label in _CORNER_LABELS:
            cv2.circle(vis, (x, y), 10, (0, 0, 255), 2)
            cv2.putText(vis, hole.label, (x - 15, y - 15), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 2)
        elif hole.covered:
            cv2.circle(vis, (x, y), 4, (0, 255, 255), -1)
        else:
            cv2.circle(vis, (x, y), 3, (0, 255, 0), -1)
    return vis


def main():
    if len(sys.argv) != 3:
        print(f"usage: {sys.argv[0]} <입력 원본 이미지> <출력 이미지>")
        sys.exit(1)

    raw = load(sys.argv[1])
    roi_image = crop_to_roi(raw)
    _, holes = all_holes_from_image(roi_image)
    boxes = detect_parts(roi_image)
    match_holes_to_boxes(holes, boxes)

    vis = draw(roi_image, holes, boxes)
    save(sys.argv[2], vis)

    covered = sum(1 for h in holes if h.covered)
    print(f"저장됨: {sys.argv[2]} (핀홀 {len(holes)}개 중 {covered}개 장착됨, 부품 박스 {len(boxes)}개)")


if __name__ == "__main__":
    main()
