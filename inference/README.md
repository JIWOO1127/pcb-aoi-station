# 추론 서비스 (Python)

- 담당: 
- 기술: Python, FastAPI, YOLO
- 포트: 8001

스테이션(C#)이 정렬된 보드 이미지를 보내면, **어느 핀홀에 어떤 부품이 꽂혀 있는지**를
반환한다. 장착 상태가 맞는지 틀린지 "판정"(PASS/FAIL)은 하지 않는다 — 그건 스테이션의 몫이다.
모델 가중치와 데이터셋은 Git에 올리지 않는다.

API 계약(요청/응답 형식)은 [docs/api-contract.md](../docs/api-contract.md) 참고.

## 처리 흐름

이미지 한 장이 들어와서 `{pinhole, class, confidence}` 목록이 나가기까지, 5단계를 거친다.

```mermaid
flowchart TD
    IMG[이미지 JPEG] --> DECODE[0. 디코딩 + 고정 ROI 크롭<br/>roi.crop_to_roi<br/>카메라·지그 고정 -&gt; 보드 대략 위치는 고정]
    DECODE --> CORNERS
    DECODE --> YOLO

    subgraph S1["1. 보드 모서리 찾기 (TODO)"]
        CORNERS[네 모서리 핀홀<br/>A0·A13·U0·U13의<br/>픽셀 좌표 검출]
    end

    subgraph S2["2. 부품 탐지 (TODO)"]
        YOLO[YOLO로 부품 박스<br/>픽셀 사각형 + 종류·신뢰도]
    end

    CORNERS --> CALIB[3. 보정<br/>calibration.py<br/>네 점 → 호모그래피]
    CALIB --> HOLES[282개 핀홀의<br/>기대 픽셀 위치 계산<br/>board.all_holes]
    HOLES --> MATCH[4. 홀-박스 대조<br/>board.match_holes_to_boxes<br/>홀 위치가 박스 안에 들어가면<br/>그 홀 = 가려짐 장착됨]
    YOLO --> MATCH
    MATCH --> OUT[가려진 홀만 추려서<br/>parts 배열로 응답]
```

**왜 이런 순서인가**
- 카메라·촬영 지그가 고정돼 있어서, 보드가 이미지 안에서 대략 어디쯤 나오는지는 항상 같다. 그래서 원본 이미지를 그대로 쓰지 않고, 먼저 고정된 사각형(ROI)으로 크롭해서 보드가 있는 부분만 남긴다.
- 다만 크롭 후에도 보드 위치·기울기·크기가 미세하게(픽셀 단위로) 조금씩 다르다. 그래서 핀홀 픽셀 위치를 고정값으로 완전히 하드코딩할 수는 없고, **크롭된 이미지마다 다시 계산**해야 한다.
- 대신 "보드 전체가 항상 크롭 영역 안에 다 들어온다"는 게 보장되어 있으므로, 매번 네 모서리 핀홀(A0·A13·U0·U13)의 픽셀 좌표만 찾으면, 나머지 278개 핀홀 위치는 계산(호모그래피)으로 구할 수 있다.
- "박스 중심과 가장 가까운 핀홀 하나"를 고르는 대신, 반대로 "핀홀마다 어떤 박스 안에 들어가는지"를 본다. 이러면 저항처럼 다리(핀) 두 개짜리 부품이 핀홀 두 개에 걸쳐 있어도 둘 다 자연스럽게 잡힌다.

## 파일 구성

| 파일 | 역할 | 상태 |
|---|---|---|
| [app/main.py](app/main.py) | FastAPI 앱. `/health`, `/infer` 엔드포인트 | 뼈대 완성, TODO 3곳 남음 |
| [app/roi.py](app/roi.py) | 고정 카메라·지그 기준, 원본 이미지에서 보드가 있는 대략적 영역(ROI)을 크롭 | ROI 좌표 실측 전 (TODO) |
| [app/pinhole_grid.py](app/pinhole_grid.py) | 핀홀 282개의 그리드 정의(행 A~U, 열 0~13)와 라벨 변환 | 완성 |
| [app/calibration.py](app/calibration.py) | 네 모서리 핀홀 픽셀 좌표 → 호모그래피 → 픽셀 ↔ 그리드 좌표 변환 | 완성 |
| [app/board.py](app/board.py) | 핀홀 282개를 이미지 속 위치·장착 여부와 함께 구조화, YOLO 박스와 대조 | 완성 |
| `requirements.txt` | fastapi, uvicorn, python-multipart, pydantic, numpy, opencv-python-headless | — |

## 아직 안 된 것 (TODO)

- **`roi.ROI`** ([app/roi.py](app/roi.py)): 크롭할 고정 사각형 좌표. 실제 카메라로 촬영해야 값을 채울 수 있음.
- **`find_board_corners`** ([app/main.py](app/main.py)): 크롭된 이미지에서 네 모서리 핀홀(A0·A13·U0·U13)의 픽셀 좌표를 실제로 찾는 부분. 기준 마크가 따로 있는지, 핀홀 자체를 이미지 처리로 찾아야 하는지는 실제 촬영 이미지가 있어야 확인 가능.
- **`detect_parts`** ([app/main.py](app/main.py)): YOLO 모델 로딩 및 추론 연동.

세션별 상세 진행 기록은 [WORKLOG.md](WORKLOG.md) 참고.
