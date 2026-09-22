# 추론 서비스 작업 로그

이 파일은 Claude와의 대화 내용을 대화할 때마다 정리해두는 곳입니다.
(브랜치: `inference/fastapi-service`, GitHub: https://github.com/JIWOO1127/pcb-aoi-station.git)

(루트에 있던 `worklog.md`를 이 파일로 통합함 — 아래 "머신비전 구성 개념 정리" 섹션 포함)

## 2026-09-22

- `inference/app/main.py` 생성됨 (아직 내용 없음, 빈 파일)
- 루트 [README.md](../README.md) 확인:
  - `inference/`는 Python FastAPI + YOLO 추론 서비스, 포트 8001
  - 역할: 이미지 받아서 부품별 탐지 결과(종류·신뢰도·박스)만 반환
  - **판정 로직은 여기서 하지 않는다** — 판정은 station(C#)만 담당
  - API: 스테이션이 `POST /infer` 로 정렬된 이미지를 보내면, 부품별 종류·신뢰도·박스를 응답
  - 저장소 구조상 `inference/app/`(FastAPI 서비스), `inference/training/`(학습 스크립트), `inference/requirements.txt` 로 구성 예정
- `/infer` API 계약 확정 (→ [docs/api-contract.md](../docs/api-contract.md)):
  - 요청: `multipart/form-data`로 이미지 파일 (형식 확정 아님, 임시 결정)
  - 응답: 부품별 `{pinhole, class, confidence}` 배열
  - **README 초안과 차이점**: 기존엔 추론 서비스가 픽셀 박스를 반환하고 스테이션이 좌표 비교했는데, 이제 추론 서비스가 픽셀→핀홀 라벨(예: "J14") 변환까지 직접 해서 반환하는 것으로 변경. 판정은 여전히 스테이션 몫.
- `inference/app/main.py` 뼈대 작성: `/health`, `/infer` 엔드포인트. YOLO 추론과 핀홀 변환 로직은 `run_inference()`에 TODO로 남겨둠 (아직 미구현)
- `inference/requirements.txt` 생성 (fastapi, uvicorn, python-multipart, pydantic)
- 이미지 형식 **JPEG로 확정** (`image/jpeg`). `/infer`에서 content-type이 JPEG가 아니면 415 에러 반환하도록 검증 추가. [docs/api-contract.md](../docs/api-contract.md), [app/main.py](app/main.py) 반영
- 핀홀 그리드 정의 및 스냅 로직 작성 ([app/pinhole_grid.py](app/pinhole_grid.py)):
  - 사용자가 제공한 좌표표 기준: **행(row) = A~U (21개, 위→아래), 열(column) = 0~13 (14개, 왼쪽→오른쪽)**. 라벨은 "행글자+열숫자" (예: "J9")
  - 열 구조: 0·1 = 왼쪽 전원 레일, 2~6 = 왼쪽 단자부, 7~11 = 오른쪽 단자부, 12·13 = 오른쪽 전원 레일
  - E, K, Q 행은 전원 레일이 없어서 0·1·12·13열 핀홀이 존재하지 않음 (해당 행은 2~11열만 존재) → 전체 핀홀 282개
  - `snap_to_pinhole(row_index, col_index)`: 그리드 좌표(실수)를 가장 가까운 **실제 존재하는** 핀홀 라벨로 clamp/snap. 잘못된 좌표(E0 등)로는 절대 안 나가도록 보장
  - (처음엔 행/열을 반대로 짰다가 — 즉 글자를 열, 숫자를 행으로 — 사용자가 좌표표 이미지를 보내줘서 바로잡음)
  - **미해결**: YOLO 박스의 "픽셀 좌표" → 이 "그리드 좌표(row_index, col_index)"로 바꾸는 보정(calibration) 단계가 아직 없음. 스테이션이 기준 마크로 정렬해서 보내지만 촬영마다 핀홀 픽셀 위치가 미묘하게 달라서, 고정 픽셀 원점/간격을 하드코딩할 수 없음. 이미지별로 기준 마크(또는 핀홀 자체)를 다시 검출해서 homography/스케일을 계산하는 단계 필요. (물리적 핀홀 간격은 실제 브레드보드와 동일한 2.54mm 피치)
  - `docs/api-contract.md`의 `pinhole` 필드 예시를 "J14"(존재 안 함) → "J9"로 수정
- 스테이션 쪽 확인: **이미지에 보정 메타데이터(기준 마크·모서리 좌표 등)는 안 실려 옴, 이미지 파일만 옴.** 대신 **보드 전체(네 모서리 다)가 항상 이미지 안에 다 보임** — 일부만 잘리는 경우는 없음. 즉 보드 모서리를 이미지 안에서 우리가 직접 찾아야 함
- 픽셀 ↔ 그리드 좌표 보정기 작성 ([app/calibration.py](app/calibration.py)):
  - 아이디어: 보드 물리적 모서리(가장자리) 대신, **네 "모서리 핀홀" 자체**(A0, A13, U0, U13)의 픽셀 좌표를 기준점으로 삼음 → 마진(모서리~첫 핀홀 사이 여백) 추정치를 몰라도 됨
  - `GridCalibration.from_corners(top_left, top_right, bottom_left, bottom_right)`: 네 모서리 핀홀 픽셀 좌표로 호모그래피(원근 변환) 행렬을 계산 (OpenCV 없이 numpy로 직접 풂)
  - `pixel_to_grid(x, y)`: 픽셀 → (row_index, col_index). `grid_to_pixel(row, col)`: 그 반대 (역행렬)
  - `requirements.txt`에 `numpy` 추가
- 핀홀 282개 전체를 구조화하는 [app/board.py](app/board.py) 작성 (사용자 요청: "hole 정보를 우리가 알아야 해 — 이미지에서의 위치, YOLO에 의해 가려졌는지 여부"):
  - `Hole`: 라벨·행·열·(이 이미지 기준) 기대 픽셀 위치·부품 종류·신뢰도. `covered` 프로퍼티로 장착 여부 확인
  - `all_holes(calibration)`: 282개 핀홀 전부를 기대 픽셀 위치와 함께 생성
  - `DetectedBox`(YOLO 박스, 픽셀 사각형) + `match_holes_to_boxes(holes, boxes)`: 각 홀의 기대 픽셀 위치가 어떤 박스 안에 들어가면 그 홀을 "가려짐"으로 표시. 박스 중심 하나로 핀홀 하나만 고르는 방식과 달리, 부품 하나가 다리 여러 개(핀홀 여러 개)에 걸치는 경우도 자연스럽게 처리됨
  - 가짜 좌표로 end-to-end 테스트 완료: 282개 생성, 모서리 핀홀 픽셀이 지정값과 정확히 일치, 가짜 박스로 특정 홀 하나만 정확히 "covered" 처리되는 것 확인
- [app/main.py](app/main.py)의 `run_inference`를 실제 파이프라인으로 연결: 모서리 찾기(TODO) → YOLO 탐지(TODO) → `GridCalibration` → `all_holes` → `match_holes_to_boxes` → 가려진 홀만 `PartResult` 목록으로 반환
- **카메라·지그 고정 확인**: 사용자 확인 — "들어오는 이미지는 고정" = 카메라·지그가 고정돼 있어서 해상도·프레임이 항상 동일함. 그래서 보드가 대략 나오는 영역도 고정된 사각형(ROI)으로 미리 크롭 가능. (다만 이 크롭은 "대강 보드 영역만 남기기"용이고, 홀 픽셀 위치가 미세하게 달라지는 부분은 여전히 `calibration.py`가 크롭된 이미지 안에서 보정)
  - [app/roi.py](app/roi.py) 작성: `ROI` 상수(x1,y1,x2,y2) + `crop_to_roi(image)`. **ROI 좌표는 아직 실측 전이라 `None`** — 실제 촬영 이미지 확보되면 채워야 함
  - [app/main.py](app/main.py): 이미지 디코딩(`cv2.imdecode`) → `crop_to_roi` → `find_board_corners`/`detect_parts`(둘 다 크롭된 이미지 기준 좌표로 동작하도록 시그니처 변경) 순으로 파이프라인 앞단에 크롭 단계 추가
  - `requirements.txt`에 `opencv-python-headless` 추가 (이미지 디코딩·크롭·향후 CV 작업용)
- 다음 할 일:
  - `roi.ROI`: 실제 카메라로 촬영 후 크롭 좌표 채우기
  - `find_board_corners`: 크롭된 이미지에서 네 모서리 핀홀(A0/A13/U0/U13)의 픽셀 좌표를 실제로 찾는 로직 (기준 마크가 있는지, 핀홀 자체를 검출해야 하는지 실제 촬영 이미지로 확인 필요)
  - `detect_parts`: YOLO 모델 연동

### 머신비전 구성 개념 정리

무엇이 딥러닝이고 무엇이 규칙 기반(전통적 이미지 처리)인지 헷갈리지 않도록 정리:

- **딥러닝**: YOLO 객체 탐지 — 이미지에서 부품 종류·위치(박스)·신뢰도를 검출. (`detect_parts`, 아직 미구현)
- **전통적 머신비전(규칙 기반)**: 기준 마크/핀홀 자체를 찾는 것, 원근 보정(homography)으로 픽셀 → 그리드 좌표 변환. (`find_board_corners`, `calibration.py`)
- **후처리(순수 계산)**: YOLO 박스와 핀홀 위치를 대조해서 "어느 핀홀이 가려졌는지" 구조화 (`board.py`), 그리드 좌표를 실제 존재하는 핀홀 라벨로 스냅 (`pinhole_grid.snap_to_pinhole`)
- **FastAPI**: 머신비전 모델이 아니라, C# 스테이션과 Python 추론 코드를 연결하는 API 서버일 뿐
- **PASS/FAIL 판정**: 딥러닝도 이 서비스도 아니고, C# 스테이션의 규칙 엔진이 담당

즉 "브레드보드 구조화" 전체가 머신비전 작업이긴 하지만, 그 안에서도 딥러닝(부품 인식)과 기하학/OpenCV(좌표 변환)와 순수 후처리(구조화)가 나뉘어 있다. 나중에 모델이 핀홀·단자 키포인트까지 직접 검출하도록 학습시키면 그 부분도 딥러닝이 되겠지만, 검출 좌표를 핀홀 이름으로 바꾸는 마지막 단계는 그래도 규칙 기반으로 남을 수 있다.

### 개발 환경 확인

- VS Code/Pylance에서 `Import "fastapi" could not be resolved` 경고 발생.
- `pcb-aoi` Conda 환경(`C:\miniforge4\envs\pcb-aoi\python.exe`, Python 3.11.16)은 존재하지만 `fastapi` 패키지가 아직 설치되지 않은 것을 확인함.
- `inference/requirements.txt`를 해당 환경에 설치하고 VS Code 인터프리터를 `pcb-aoi`로 선택해야 함.
