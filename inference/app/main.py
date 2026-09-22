"""추론 서비스 (FastAPI)

스테이션(C#)이 정렬된 이미지를 보내면, 부품별로
- 어느 핀홀에 꽂혀 있는지 (pinhole)
- 어떤 부품인지 (class)
- 신뢰도 (confidence)
를 반환한다. 장착 상태가 맞는지 틀린지 "판정"하는 것은 스테이션의 몫이며,
이 서비스는 절대 판정하지 않는다. (docs/api-contract.md 참고)
"""

from typing import List

import cv2
import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from pydantic import BaseModel, ConfigDict, Field

from board import DetectedBox, all_holes, match_holes_to_boxes
from calibration import GridCalibration
from roi import crop_to_roi

app = FastAPI(title="PCB AOI 추론 서비스")


class PartResult(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    pinhole: str  # 핀홀 라벨 (예: "J9")
    part_class: str = Field(alias="class")  # 탐지된 부품 종류
    confidence: float  # 0~1


class InferResponse(BaseModel):
    parts: List[PartResult]


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.post("/infer", response_model=InferResponse)
async def infer(image: UploadFile = File(...)) -> InferResponse:
    if image.content_type != "image/jpeg":
        raise HTTPException(
            status_code=415, detail="image는 JPEG(image/jpeg)만 지원합니다."
        )

    image_bytes = await image.read()
    parts = run_inference(image_bytes)
    return InferResponse(parts=parts)


def run_inference(image_bytes: bytes) -> List[PartResult]:
    """이미지 한 장을 받아, 핀홀별 장착 부품을 반환한다.

    단계:
    0. 이미지를 디코딩하고, 고정 ROI로 크롭한다. 카메라·지그가 고정돼 있어서
       보드가 대략 나오는 영역도 고정 사각형으로 미리 잘라낼 수 있다. (roi.py)
    1. 보드 네 모서리 핀홀(A0, A13, U0, U13)의 픽셀 좌표를 찾는다.        [TODO]
    2. YOLO로 부품을 탐지해 박스(픽셀 좌표)를 얻는다.                    [TODO]
    3. 핀홀 282개의 기대 픽셀 위치를 계산하고, 박스와 대조해 어느 핀홀이
       가려져 있는지(=장착돼 있는지) 구한다. (board.py, calibration.py로 구현됨)
    4. 가려진(=장착된) 핀홀만 골라 응답으로 만든다.

    1~3단계는 모두 크롭된 이미지 기준 픽셀 좌표로 이뤄진다.
    """
    image = _decode_image(image_bytes)
    roi_image = crop_to_roi(image)  # TODO: roi.py의 ROI 좌표 실측 필요

    corners = find_board_corners(roi_image)  # TODO
    boxes = detect_parts(roi_image)  # TODO

    calibration = GridCalibration.from_corners(**corners)
    holes = all_holes(calibration)
    match_holes_to_boxes(holes, boxes)

    return [
        PartResult(pinhole=hole.label, part_class=hole.part_class, confidence=hole.confidence)
        for hole in holes
        if hole.covered
    ]


def _decode_image(image_bytes: bytes) -> np.ndarray:
    """JPEG 바이트를 BGR 픽셀 배열로 디코딩한다."""
    array = np.frombuffer(image_bytes, dtype=np.uint8)
    image = cv2.imdecode(array, cv2.IMREAD_COLOR)
    if image is None:
        raise HTTPException(status_code=400, detail="이미지를 디코딩할 수 없습니다.")
    return image


def find_board_corners(roi_image: np.ndarray) -> dict:
    """크롭된 이미지에서 보드 네 모서리 핀홀(A0, A13, U0, U13)의 픽셀 좌표를 찾는다.

    TODO: 실제 촬영 이미지로 확인 필요 — 모서리에 별도 기준 마크가 있는지,
    아니면 핀홀 자체를 검출해서 가장자리 4개를 골라야 하는지 등.

    반환값은 GridCalibration.from_corners()에 그대로 넣을 수 있는 형태:
    {"top_left": (x, y), "top_right": (x, y), "bottom_left": (x, y), "bottom_right": (x, y)}
    """
    raise NotImplementedError("보드 모서리 핀홀 검출 필요")


def detect_parts(roi_image: np.ndarray) -> List[DetectedBox]:
    """크롭된 이미지에서 YOLO 모델로 부품을 탐지해 박스 목록을 반환한다.

    TODO: YOLO 모델 로딩 및 추론.
    """
    raise NotImplementedError("YOLO 모델 연동 필요")
