"""Faster R-CNN 실험 설정.

노트북(train.ipynb, test.ipynb)은 모든 설정값을 여기서 읽는다.
Colab과 로컬을 자동으로 구분해 경로를 고른다. 환경변수로 덮어쓸 수도 있다.
"""
import os
import sys
from pathlib import Path

IN_COLAB = "google.colab" in sys.modules
HERE = Path(__file__).resolve().parent          # 이 파일이 있는 폴더 (faster_rcnn/)

# ── 클래스 ────────────────────────────────────────────────
# 학습 json의 category_id 1~8 순서와 같아야 한다 (RTMDet과 동일).
# 기존 가중치는 json의 id를 그대로 라벨로 학습했으므로 순서를 바꾸면 안 된다.
# 노트북에서 common.check_train_classes()가 이 순서를 검사한다.
CLASSES = [
    "black_wire", "capacitor", "ic_chip", "led",
    "red_wire", "resistance330", "resistance470", "white_wire",
]
NUM_CLASSES = len(CLASSES) + 1                  # 0번은 배경

# ── 경로 ─────────────────────────────────────────────────
if IN_COLAB:
    _DRIVE = Path("/content/drive/MyDrive")
    _DATA_ROOT = _DRIVE / "Circuit testing 2 -test.v1i.coco"
    _NEW_TEST_DIR = _DRIVE / "Circuit testing 2 -test.coco" / "test"
    _OUTPUT_DIR = HERE                          # /MyDrive/pcb-aoi/faster_rcnn (기존 가중치 위치)
else:
    _DATASET = HERE.parent.parent / "dataset"   # inference/dataset (RTMDet과 공유)
    _DATA_ROOT = _DATASET / "Circuit testing 2 -test.v1i.coco"
    _NEW_TEST_DIR = _DATASET / "Circuit testing 2 -test.coco" / "test"
    _OUTPUT_DIR = HERE / "outputs"

DATA_ROOT = Path(os.environ.get("PCB_DATA_ROOT", _DATA_ROOT))            # train/valid/test 폴더가 있는 곳
OLD_TEST_DIR = DATA_ROOT / "test"                                         # 구 테스트 8장
NEW_TEST_DIR = Path(os.environ.get("PCB_NEW_TEST_DIR", _NEW_TEST_DIR))   # 새 테스트 22장
OUTPUT_DIR = Path(os.environ.get("PCB_OUTPUT_DIR", _OUTPUT_DIR))         # 가중치 (Git 제외)
RESULTS_DIR = HERE / "results"                                            # 지표 json, 학습 로그 (Git 포함)

# 평가에 쓸 가중치. 다시 학습했다면 train.ipynb가 알려주는 새 파일명으로 바꾼다.
# (초기 가중치 best_fasterrcnn_20260923.pth는 소실되어 같은 설정 + seed 42로 재학습했다)
WEIGHTS_NAME = "best_fasterrcnn_20260923_1007.pth"
WEIGHTS = OUTPUT_DIR / WEIGHTS_NAME

# ── 새 테스트셋 클래스 이름 수정 ────────────────────────────
# 평가는 json의 id를 이름 기준으로 학습 라벨에 다시 매핑한다.
# 2026-09-23 평가에서는 새 테스트 json의 id 1~8 이름이 학습 json과 모두 같았다 (수정 불필요).
# 틀린 이름이 CLASSES에 없는 이름 하나뿐이면 자동으로 고쳐진다 (경고 출력).
# 자동 수정이 안 될 때만 {json의 id: "올바른 이름"} 형태로 적는다. 예: {8: "white_wire"}
NEW_TEST_ID_FIX = {}

# ── 학습 (2026-09-23 학습과 같은 값) ───────────────────────
MODEL_NAME = "fasterrcnn_resnet50_fpn"          # v1, COCO 사전학습, 분류기만 교체
EPOCHS = 30
BATCH_SIZE = 2
LR = 0.005
MOMENTUM = 0.9
WEIGHT_DECAY = 0.0005
NUM_WORKERS = 2
SEED = 42            # 기존 가중치는 seed 없이 학습했다. 재학습해도 같은 수치가 나오지 않는다.
# 입력 크기: torchvision 기본값 (짧은 변 800, 긴 변 최대 1333으로 모델 내부에서 리사이즈)
# 데이터 증강: 없음 (to_tensor만). YOLO와 비교할 때 한계로 기록.

# ── 평가 ─────────────────────────────────────────────────
IOU_TH = 0.5         # 혼동행렬·P/R/F1 매칭 기준
SCORE_TH = 0.5       # 혼동행렬·P/R/F1에 쓰는 신뢰도 하한 (mAP는 모든 예측 사용)
LATENCY_WARMUP = 5
LATENCY_RUNS = 50


def summary():
    """노트북에서 현재 설정을 확인할 때 쓴다."""
    return {
        "IN_COLAB": IN_COLAB,
        "DATA_ROOT": str(DATA_ROOT),
        "OLD_TEST_DIR": str(OLD_TEST_DIR),
        "NEW_TEST_DIR": str(NEW_TEST_DIR),
        "OUTPUT_DIR": str(OUTPUT_DIR),
        "RESULTS_DIR": str(RESULTS_DIR),
        "WEIGHTS": str(WEIGHTS),
    }
