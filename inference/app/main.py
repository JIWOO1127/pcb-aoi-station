"""추론 서비스 (FastAPI)

스테이션(C#)이 정렬된 이미지를 보내면, 부품별로
- 어느 핀홀에 꽂혀 있는지 (pinhole)
- 어떤 부품인지 (class)
- 신뢰도 (confidence)
를 반환한다. 장착 상태가 맞는지 틀린지 "판정"하는 것은 스테이션의 몫이며,
이 서비스는 절대 판정하지 않는다. (docs/api-contract.md 참고)
"""

from typing import List

from fastapi import FastAPI, File, HTTPException, UploadFile
from pydantic import BaseModel, ConfigDict, Field

app = FastAPI(title="PCB AOI 추론 서비스")


class PartResult(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    pinhole: str  # 브레드보드 핀홀 라벨 (예: "J14")
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
    """YOLO로 부품을 탐지하고, 픽셀 좌표를 핀홀 라벨로 변환한다.

    TODO:
    - YOLO 모델 로딩 및 추론
    - 탐지된 박스(픽셀 좌표) -> 브레드보드 핀홀 라벨 변환
    """
    raise NotImplementedError("YOLO 모델 연동 필요")
