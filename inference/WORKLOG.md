# 추론 서비스 작업 로그

이 파일은 Claude와의 대화 내용을 대화할 때마다 정리해두는 곳입니다.
(브랜치: `inference/fastapi-service`)

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
- 다음 할 일:
  - YOLO 모델 연동 (`run_inference` 구현)
  - 픽셀 좌표 → 브레드보드 핀홀 라벨 변환 로직 설계
