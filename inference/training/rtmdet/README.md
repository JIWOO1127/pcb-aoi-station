# PCB RTMDet-m 학습 및 평가

Roboflow COCO 데이터셋의 8개 부품/배선 클래스를 RTMDet-m으로 학습한 기록입니다. `train.ipynb`는 100 epoch 학습을 수행하고, `test.ipynb`는 저장된 체크포인트로 COCO 평가와 이미지 추론을 수행합니다.

## 공유할 파일

| 경로 | 용도 |
| --- | --- |
| `train.ipynb`, `test.ipynb` | 학습 및 테스트 절차 |
| `config.py` | 8개 클래스, 전처리, 데이터 경로, 평가 설정을 포함한 재현용 설정 |
| `requirements.txt` | 사용한 Python 패키지 버전 |
| 이 `README.md` | 실행 방법과 데이터셋 출처 |

데이터셋은 Git에서 제외합니다. 실행하는 사람은 [Roboflow의 Circuit testing 2 -test v1](https://universe.roboflow.com/-388ju/circuit-testing-2-test)에서 **COCO 형식**으로 받아 `inference/dataset/Circuit testing 2 -test.v1i.coco/`에 압축을 풀어야 합니다. 그 안에 `train/`, `valid/`, `test/` 폴더와 각 폴더의 `_annotations.coco.json`이 있어야 합니다. 원본 데이터셋의 라이선스는 CC BY 4.0입니다.

`configs/`는 `train.ipynb`가 OpenMIM으로 내려받는 COCO 기본 설정과 사전학습 가중치, 학습 중 생성한 설정을 담은 로컬 작업 디렉터리입니다. Git에서는 제외하며, 학습 노트북을 처음부터 실행하면 다시 생성됩니다. `outputs/`의 날짜별 로그, `epoch_80.pth`, `epoch_90.pth`, `last_checkpoint`, 생성된 설정 파일 및 `test_outputs/`도 Git에서 제외합니다. 로컬 파일은 그대로 둘 수 있습니다. 이전 epoch부터 **이어 학습**하려면 해당 epoch의 체크포인트와 별도 학습 상태가 필요합니다.

최종 모델 `epoch_100.pth`는 용량이 크므로 Git에 올리지 않고 별도로 공유합니다. 공유받은 파일을 `inference/training/rtmdet/outputs/epoch_100.pth`에 두면 `test.ipynb`를 실행할 수 있습니다. 모델 없이도 `train.ipynb`를 처음부터 실행해 체크포인트를 새로 만들 수 있습니다.

## 환경과 실행

학습에 사용한 환경은 Python 3.10.11, PyTorch 2.1.2 + CUDA 12.1, MMCV 2.1.0, MMEngine 0.10.7, MMDetection 3.3.0이었습니다. `requirements.txt`는 로컬 `.venv`의 설치 메타데이터에 맞췄습니다. PyTorch와 MMCV는 OS/CUDA 조합에 맞는 바이너리를 먼저 설치한 뒤 나머지 패키지를 설치하세요. 현재 저장소의 `.venv`는 원래 Python 설치 경로를 가리키므로 다른 PC에 복사해 쓸 수 없습니다.

1. Python 3.10 가상환경을 새로 만듭니다.
2. 사용 중인 GPU와 CUDA에 맞는 PyTorch 2.1.2 및 torchvision 0.16.2를 설치합니다.
3. `openmim==0.3.9`를 설치하고 `mim install mmcv==2.1.0`을 실행합니다.
4. 이 디렉터리에서 `pip install -r requirements.txt`를 실행합니다.
5. Jupyter의 작업 디렉터리를 이 디렉터리(`inference/training/rtmdet`)로 놓고 노트북을 실행합니다. `test.ipynb`는 `config.py`와 `outputs/epoch_100.pth`를 사용합니다.

`config.py`는 이 디렉터리를 현재 작업 디렉터리로 삼아 상대경로로 COCO 데이터셋을 찾습니다. 모델 클래스 순서는 `black_wire`, `capacitor`, `ic_chip`, `led`, `red_wire`, `resistance330`, `resistance470`, `white_wire`입니다. 테스트 노트북은 CUDA GPU를 전제로 작성되었습니다.

기존 노트북 실행 기록에서 100 epoch 검증 COCO bbox mAP(0.50:0.95)는 **0.661**, 8장 테스트셋의 bbox mAP는 **0.744**였습니다. 이 값은 저장된 과거 실행 결과이며, 정리한 노트북을 새 `config.py`로 다시 실행해 얻은 값은 아닙니다.

## Git에 올릴 때

`.gitignore`는 `inference/dataset/`, 가상환경, `configs/`, `test_outputs/`, 생성된 `outputs/`와 체크포인트를 제외합니다. 데이터셋과 모델 가중치는 별도로 공유받아 위에 적힌 경로에 배치하세요.
