"""추론 서비스 (FastAPI)

스테이션(C#)이 정렬된 이미지를 보내면, 부품별로
- 어느 핀홀에 꽂혀 있는지 (pinhole)
- 어떤 부품인지 (class)
- 신뢰도 (confidence)
를 반환한다. 장착 상태가 맞는지 틀린지 "판정"하는 것은 스테이션의 몫이며,
이 서비스는 절대 판정하지 않는다. (docs/api-contract.md 참고)
"""

import base64
import os
from pathlib import Path
from typing import List

import cv2
import numpy as np
from fastapi import FastAPI, File, HTTPException, Query, UploadFile
from pydantic import BaseModel, ConfigDict, Field
from ultralytics import YOLO

from board import DetectedBox, all_holes_from_image, match_holes_to_boxes
from roi import crop_to_roi

app = FastAPI(title="PCB AOI 추론 서비스")

# 부품 탐지 YOLO 모델(yolo8n.pt, 8종 학습됨: black_wire/capacitor/ic_chip/led/
# red_wire/resistance330/resistance470/white_wire). 환경변수로 다른 경로의
# 모델(예: RTMDet 등 추후 비교용)을 넣을 수 있게 기본값만 코드에 둔다.
_MODEL_PATH = os.environ.get("YOLO_MODEL_PATH", str(Path(__file__).parent / "models" / "yolo8n.pt"))
_CONFIDENCE_THRESHOLD = 0.25

_model: YOLO | None = None


class PartResult(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    pinhole: str  # 핀홀 라벨 (예: "J9")
    part_class: str = Field(alias="class")  # 탐지된 부품 종류
    confidence: float  # 0~1


class OverlayPinhole(BaseModel):
    pinhole: str  # 핀홀 라벨 (예: "J9")
    x: float  # 크롭된 이미지 기준 픽셀 x좌표
    y: float  # 크롭된 이미지 기준 픽셀 y좌표


class OverlayBox(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    part_class: str = Field(alias="class")  # YOLO가 탐지한 부품 종류
    confidence: float  # 0~1
    x1: float  # 크롭된 이미지 기준 픽셀 좌표
    y1: float
    x2: float
    y2: float


class InferOverlay(BaseModel):
    """/infer?overlay=true 일 때만 붙는 시각 확인용 정보. 좌표는 모두 imageJpeg 기준."""

    model_config = ConfigDict(populate_by_name=True)

    width: int  # 크롭된 이미지(roi_image) 가로 픽셀
    height: int  # 크롭된 이미지 세로 픽셀
    image_jpeg: str = Field(alias="imageJpeg")  # 크롭된 이미지, JPEG(품질 90) base64
    pinholes: List[OverlayPinhole]  # 핀홀 282개 전부의 기대 픽셀 위치
    boxes: List[OverlayBox]  # YOLO 탐지 박스 전부


class InferResponse(BaseModel):
    parts: List[PartResult]
    # overlay=true일 때만 채운다. 비어 있으면 응답 JSON에 아예 나오지 않는다
    # (/infer의 response_model_exclude_unset) → overlay=false 응답은 예전과 같다.
    overlay: InferOverlay | None = None


class DebugPinhole(BaseModel):
    pinhole: str  # 핀홀 라벨 (예: "J9")
    x: float  # 크롭된 이미지 기준 픽셀 x좌표
    y: float  # 크롭된 이미지 기준 픽셀 y좌표


class InferDebugResponse(BaseModel):
    pinholes: List[DebugPinhole]


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.post("/infer", response_model=InferResponse, response_model_exclude_unset=True)
async def infer(
    image: UploadFile = File(...),
    overlay: bool = Query(False, description="true면 크롭 이미지·핀홀 좌표·YOLO 박스를 함께 반환"),
) -> InferResponse:
    if image.content_type != "image/jpeg":
        raise HTTPException(
            status_code=415, detail="image는 JPEG(image/jpeg)만 지원합니다."
        )

    image_bytes = await image.read()
    if not overlay:
        parts = run_inference(image_bytes)
        return InferResponse(parts=parts)

    # overlay: run_inference와 같은 단계를 한 번만 돌리고, 중간 결과(크롭 이미지·
    # 핀홀·박스)를 응답에 함께 싣는다.
    decoded = _decode_image(image_bytes)
    roi_image, img_scale, label_anchors = crop_to_roi(decoded)
    _, holes = all_holes_from_image(roi_image, img_scale, label_anchors)
    boxes = detect_parts(roi_image)
    match_holes_to_boxes(holes, boxes)

    parts = [
        PartResult(pinhole=hole.label, part_class=hole.part_class, confidence=hole.confidence)
        for hole in holes
        if hole.covered
    ]
    ok, jpeg = cv2.imencode(".jpg", roi_image, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not ok:
        raise HTTPException(status_code=500, detail="크롭 이미지를 JPEG로 인코딩할 수 없습니다.")

    return InferResponse(
        parts=parts,
        overlay=InferOverlay(
            width=roi_image.shape[1],
            height=roi_image.shape[0],
            image_jpeg=base64.b64encode(jpeg.tobytes()).decode("ascii"),
            pinholes=[OverlayPinhole(pinhole=h.label, x=h.pixel[0], y=h.pixel[1]) for h in holes],
            boxes=[
                OverlayBox(part_class=b.part_class, confidence=b.confidence, x1=b.x1, y1=b.y1, x2=b.x2, y2=b.y2)
                for b in boxes
            ],
        ),
    )


@app.post("/infer/debug", response_model=InferDebugResponse)
async def infer_debug(image: UploadFile = File(...)) -> InferDebugResponse:
    """임시 테스트용 엔드포인트: `detect_parts`(YOLO)가 아직 없어서, /infer처럼
    "가려진 핀홀만" 거르는 대신 검출된 핀홀 282개 전부의 픽셀 위치를 반환한다.

    스테이션에서 실제로 이미지를 보내 크롭+핀홀검출 파이프라인이 잘 도는지
    확인하는 용도. detect_parts가 완성되면 지워도 된다.
    """
    if image.content_type != "image/jpeg":
        raise HTTPException(
            status_code=415, detail="image는 JPEG(image/jpeg)만 지원합니다."
        )

    image_bytes = await image.read()
    decoded = _decode_image(image_bytes)
    roi_image, img_scale, label_anchors = crop_to_roi(decoded)
    _, holes = all_holes_from_image(roi_image, img_scale, label_anchors)

    return InferDebugResponse(
        pinholes=[DebugPinhole(pinhole=h.label, x=h.pixel[0], y=h.pixel[1]) for h in holes]
    )


def run_inference(image_bytes: bytes) -> List[PartResult]:
    """이미지 한 장을 받아, 핀홀별 장착 부품을 반환한다.

    단계:
    0. 이미지를 디코딩하고, 고정+레일 검출 ROI로 크롭한다. (roi.py)
    1. 크롭된 이미지에서 홀(구멍)을 직접 검출해, 핀홀 282개의 기대 픽셀
       위치를 계산한다. (board.py, board_detect.py)
    2. 크롭된 이미지로 YOLO 부품을 탐지해 박스(픽셀 좌표)를 얻는다. (detect_parts)
    3. 핀홀 위치와 박스를 대조해 어느 핀홀이 가려져 있는지(=장착돼 있는지) 구한다.
    4. 가려진(=장착된) 핀홀만 골라 응답으로 만든다.

    1 이후 단계는 모두 크롭된 이미지 기준 픽셀 좌표로 이뤄진다.
    """
    image = _decode_image(image_bytes)
    roi_image, img_scale, label_anchors = crop_to_roi(image)

    _, holes = all_holes_from_image(roi_image, img_scale, label_anchors)
    boxes = detect_parts(roi_image)
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


def _get_model() -> YOLO:
    """YOLO 모델을 최초 1번만 로딩해서 재사용한다 (요청마다 다시 로딩하면 느림)."""
    global _model
    if _model is None:
        _model = YOLO(_MODEL_PATH)
    return _model


def detect_parts(roi_image: np.ndarray) -> List[DetectedBox]:
    """크롭된 이미지에서 YOLO 모델로 부품을 탐지해 박스 목록을 반환한다.

    `_CONFIDENCE_THRESHOLD` 미만인 탐지는 버린다. 부품 종류(class)는 모델에
    학습된 이름(model.names)을 그대로 쓴다.
    """
    model = _get_model()
    result = model(roi_image, conf=_CONFIDENCE_THRESHOLD, verbose=False)[0]

    boxes = []
    for box in result.boxes:
        x1, y1, x2, y2 = box.xyxy[0].tolist()
        class_id = int(box.cls[0])
        boxes.append(
            DetectedBox(
                x1=x1,
                y1=y1,
                x2=x2,
                y2=y2,
                part_class=model.names[class_id],
                confidence=float(box.conf[0]),
            )
        )
    return boxes
