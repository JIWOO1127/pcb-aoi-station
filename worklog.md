# Work Log

프로젝트 진행 중 나눈 대화에서 확정된 작업 내용과 결정 사항을 날짜순으로 기록한다.

## 2026-09-22

### Git 및 작업 위치

- GitHub 저장소: `https://github.com/JIWOO1127/pcb-aoi-station.git`
- 작업 브랜치: `inference/fastapi-service`
- Python 추론 서비스 작업 위치: `inference/app/`
- FastAPI 시작 파일: `inference/app/main.py`
- `main` 브랜치에는 직접 푸시하지 않고, 작업 브랜치에서 커밋·푸시한 뒤 PR을 생성한다.

### 추론 서비스 역할

- 기술 구성: Python, FastAPI, YOLO
- 사용 포트: `8001`
- C# 스테이션으로부터 정렬된 PCB 이미지를 받는다.
- 부품 종류, 신뢰도, 바운딩 박스 등 탐지 결과만 반환한다.
- PASS/FAIL 판정은 C# 스테이션이 담당한다.
- 모델 가중치와 데이터셋은 Git에 올리지 않는다.
- API 데이터 형식을 변경할 때는 `docs/api-contract.md`를 먼저 수정하고 팀에 알린다.

### 브레드보드 핀홀 좌표 규칙

- 행은 위에서 아래로 `A~U`를 사용한다.
- 열은 왼쪽에서 오른쪽으로 `0~13`을 사용한다.
- 한 행의 구성은 `전원 레일 0~1 | 본체 2~6 | 본체 7~11 | 전원 레일 12~13`이다.
- `E`, `K`, `Q` 행에는 전원 레일 핀홀이 없다.
- 따라서 `E0`, `E1`, `E12`, `E13`, `K0`, `K1`, `K12`, `K13`, `Q0`, `Q1`, `Q12`, `Q13` 좌표는 사용하지 않는다.
- 해당 좌표가 없어도 다른 열 번호를 앞당기지 않는다.
- 전체 좌표표 이미지 v2를 제작했다.

### 기록 방식

- 앞으로 프로젝트 관련 대화, 결정 사항, 실행 결과를 이 파일에 계속 추가한다.

### Git 커밋 준비

- 현재 브랜치가 `inference/fastapi-service`임을 확인했다.
- 현재 Git이 추적하지 않는 항목: `docs/api-contract.md`, `inference/WORKLOG.md`, `inference/app/`, `inference/requirements.txt`, `worklog.md`.
- 위 파일들이 모두 이번 추론 서비스 작업에 포함되는지 확인한 뒤 스테이징하고 커밋한다.
- 권장 커밋 메시지: `[inference] FastAPI 추론 서비스 초기 구성`.
