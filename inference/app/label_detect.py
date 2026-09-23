"""세로 크롭 범위를 고정값 대신, 실크스크린에 인쇄된 행 번호 라벨("10"~"30")을
OCR로 읽어서 매번 계산한다.

`roi.Y_RANGE`(고정 픽셀 값)의 한계: 카메라 위치·줌이 사진마다 조금씩 달라서,
같은 픽셀 범위가 어떤 사진에서는 행 A~U를 넉넉히 담고 어떤 사진에서는 마지막
행(U)을 잘라버릴 수 있다 (실측: `board2.jpg`에서 마지막 행이 잘림, `board1.jpg`도
여백이 30px 남짓이라 위태로웠음 — WORKLOG.md 참고).

그래서 "10"~"30" 사이 행 번호 라벨(10,15,20,25,30)을 OCR(Tesseract)로 찾아서,
그 간격으로 실제 칸 피치를 계산하고, 위아래로 여유를 조금 두고 세로 범위를
다시 계산한다. `roi.Y_RANGE`는 이제 "대략 이 근처"라는 시작점(가로 레일 검출을
어느 대역에서 할지)으로만 쓰이고, 최종 세로 범위는 이 모듈이 정한다.

**5개 라벨을 전부 찾으려는 게 아니라, 찾은 만큼만 쓴다.** 처음엔 "10"과
"30" 둘 다 있어야만 계산했는데, 실측(`board3.jpg`)해보니 같은 보드에서도
라벨마다 폰트가 달라서("30"만 필기체 스타일) 특정 라벨 하나만 OCR이 못 읽는
경우가 있었다. 그때마다 통째로 실패 처리하고 부정확한 고정값으로 돌아가는
대신, 2개 이상 찾히면 선형회귀로 피치를 추정하고, 1개만 찾히면 기본 피치
추정값으로 나머지를 외삽한다 (WORKLOG.md 참고).

가로 검출(rail_detect)과 반대로, 이건 색상이 아니라 텍스트(숫자)를 읽는
것이라 OCR 엔진이 필요하다 — Tesseract(엔진 실행파일) + pytesseract(파이썬
바인딩). 그래도 학습된 신경망으로 뭔가를 "인식"시키는 게 아니라 정해진
숫자 목록만 찾는 것이라, 여전히 딥러닝은 아니다.
"""

import os
import sys
from pathlib import Path

import cv2
import numpy as np
import pytesseract

# OCR 검색은 roi.Y_RANGE 바깥으로 이만큼 더 넓게 본다 (카메라 위치가 달라서
# 고정값이 실제 라벨 위치를 못 담아도 찾을 수 있도록).
_OCR_SEARCH_MARGIN = 400
_MIN_OCR_CONFIDENCE = 80
# 라벨 위치에서 위아래로 이 비율(*피치)만큼 여유를 두고 자른다.
_ROW_MARGIN_RATIO = 0.75

# 각 라벨 텍스트 -> 그 라벨이 가리키는 행의 물리적 인덱스(A=0 ~ U=20).
_LABEL_ROW_INDEX = {"10": 0, "15": 5, "20": 10, "25": 15, "30": 20}

# 라벨을 1개만 찾았을 때 나머지를 외삽하는 데 쓸 기본 피치(실측 평균, px/행).
_DEFAULT_PITCH = 54.5


class LabelsNotFoundError(ValueError):
    """행 번호 라벨을 하나도 찾지 못했을 때."""


def _configure_tesseract() -> None:
    """conda 환경 기준으로 tesseract 실행파일·tessdata 경로를 자동으로 맞춘다.

    이미 TESSDATA_PREFIX가 설정돼 있거나 pytesseract가 이미 tesseract를
    찾을 수 있으면 손대지 않는다.
    """
    env_root = Path(sys.executable).parent  # conda 환경의 python.exe가 있는 폴더
    exe = env_root / "Library" / "bin" / "tesseract.exe"
    if exe.exists():
        pytesseract.pytesseract.tesseract_cmd = str(exe)
    if "TESSDATA_PREFIX" not in os.environ:
        tessdata = env_root / "share" / "tessdata"
        if tessdata.exists():
            os.environ["TESSDATA_PREFIX"] = str(tessdata)


_configure_tesseract()


def find_label_row_points(
    rotated_image: np.ndarray, x1: int, x2: int, rough_y_range: tuple[int, int]
) -> list[tuple[int, float]]:
    """rotated_image[*, x1:x2] 구간에서 "10"~"30" 라벨들을 찾는다.

    반환: [(그 라벨의 물리적 행 인덱스, y좌표), ...] — 찾은 라벨만 포함(순서 무관).
    rough_y_range 바깥으로 `_OCR_SEARCH_MARGIN`만큼 더 넓게 검색해서, 고정값이
    실제 라벨 위치를 못 담고 있어도(카메라 위치 차이) 찾을 수 있게 한다.
    """
    y1, y2 = rough_y_range
    search_y1 = max(0, y1 - _OCR_SEARCH_MARGIN)
    search_y2 = y2 + _OCR_SEARCH_MARGIN
    strip = rotated_image[search_y1:search_y2, x1:x2]
    gray = cv2.cvtColor(strip, cv2.COLOR_BGR2GRAY)

    data = pytesseract.image_to_data(
        gray,
        config="--psm 11 -c tessedit_char_whitelist=0123456789",
        output_type=pytesseract.Output.DICT,
    )

    points: list[tuple[int, float]] = []
    for i, text in enumerate(data["text"]):
        t = text.strip()
        if t not in _LABEL_ROW_INDEX:
            continue
        if int(data["conf"][i]) < _MIN_OCR_CONFIDENCE:
            continue
        y_center = data["top"][i] + data["height"][i] / 2
        points.append((_LABEL_ROW_INDEX[t], search_y1 + y_center))

    if not points:
        raise LabelsNotFoundError("행 번호 라벨을 하나도 찾지 못했습니다 (10/15/20/25/30).")
    return points


def refine_y_range(
    rotated_image: np.ndarray, x1: int, x2: int, rough_y_range: tuple[int, int]
) -> tuple[int, int]:
    """행 번호 라벨 위치로, 행 A~U를 정확히 담는 세로 범위(y1, y2)를 계산한다.

    라벨을 2개 이상 찾으면 (행 인덱스, y) 점들로 `y = m*행인덱스 + c` 선형회귀를
    해서 피치(m)와 절편(c)을 구한다. 1개만 찾으면 `_DEFAULT_PITCH`로 나머지를
    외삽한다 (라벨마다 폰트가 달라 일부만 인식되는 경우가 있어서 — 모듈
    docstring 참고).
    """
    points = find_label_row_points(rotated_image, x1, x2, rough_y_range)

    if len(points) >= 2:
        indices = np.array([p[0] for p in points], dtype=np.float64)
        ys = np.array([p[1] for p in points], dtype=np.float64)
        design = np.column_stack([indices, np.ones_like(indices)])
        pitch, intercept = np.linalg.lstsq(design, ys, rcond=None)[0]
    else:
        index0, y0 = points[0]
        pitch = _DEFAULT_PITCH
        intercept = y0 - pitch * index0

    y_top = intercept  # 행 인덱스 0(A)
    y_bottom = pitch * 20 + intercept  # 행 인덱스 20(U)
    margin = _ROW_MARGIN_RATIO * pitch
    return round(y_top - margin), round(y_bottom + margin)
