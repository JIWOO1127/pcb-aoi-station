# 스테이션 서버 작업 로그

이 파일은 Claude와의 대화 내용을 대화할 때마다 정리해두는 곳입니다.
(브랜치: `inference/fastapi-service` — 원래는 station 담당 작업 브랜치가 따로 있어야 하지만,
지금은 추론 서비스 작업 중 연동 테스트 목적으로 같이 진행함)

## 2026-09-22

- 목표: 추론 서비스(Python, `/infer`)를 스테이션(C#)이 잘 호출하고 응답(핀홀별 부품 정보)을
  잘 받아오는지 테스트해보고 싶다는 요청 → 실제 검사 흐름과는 별개로, 연동 자체를
  확인하기 위한 최소 코드를 추가함
- [Services/InferenceClient.cs](Inspection.Station/Services/InferenceClient.cs):
  - `InferPart(Pinhole, Class, Confidence)` / `InferResponse(Parts)`: 추론 서비스 응답 모양 그대로 (`docs/api-contract.md` 참고)
  - **주의**: `Inspection.Contracts`의 기존 `PartResult`(판정까지 끝난 결과, PartId·Verdict·Defect 등 포함)와는 다른 것. 얘는 판정 전, 추론 서비스가 준 원본 정보만 담음. 이름 겹치는 것 피하려고 `InferPart`/`InferResponse`로 이름 지음
  - `IInferenceClient.InferAsync(byte[] jpegBytes)`: `HttpClient`로 `/infer`에 `multipart/form-data`(필드명 `image`, `image/jpeg`)로 이미지 보내고 JSON 응답 파싱
- [Controllers/InferenceTestController.cs](Inspection.Station/Controllers/InferenceTestController.cs):
  - `POST /api/inference-test` — 이미지 파일 업로드받아서 `InferenceClient`로 추론 서비스에 넘기고 응답 그대로 반환. **실제 검사 흐름(`POST /api/trigger`)에는 아직 연결 안 됨**, 순수 연동 테스트용
- `Program.cs`: `AddHttpClient<IInferenceClient, InferenceClient>` 등록, base address는 `appsettings.json`의 `InferenceService:BaseUrl`(기본값 `http://localhost:8001`)에서 읽음
- `appsettings.json`: `InferenceService:BaseUrl` 추가
- `Inspection.Station.http`: 테스트용 요청 예시 추가 (multipart라 REST Client 확장이 안 먹으면 curl 대체 명령도 같이 적어둠)
- `dotnet build` 성공 확인 (경고 0, 오류 0)

### 테스트 방법

1. 추론 서비스 먼저 켜기 (`inference/app`에서 `uvicorn main:app --reload --port 8001`)
2. 스테이션 서버 켜기 (`station/Inspection.Station`에서 `dotnet run`, 기본 `http://localhost:5215`)
3. 아무 jpg 파일(`test.jpg`) 하나 준비
4. 호출:
   ```
   curl.exe -X POST http://localhost:5215/api/inference-test -F "image=@test.jpg;type=image/jpeg"
   ```
5. 기대 결과: 지금은 추론 서비스 쪽 `roi.ROI`가 아직 없어서 추론 서비스가 `500`을 주고, 스테이션의 `InferenceClient`는 `response.EnsureSuccessStatusCode()`에서 예외를 던짐 → 스테이션도 `500` 응답. **이게 지금 단계에서 나올 수 있는 정상적인 결과** (스테이션 -> 추론 서비스 호출 자체는 잘 되고, 추론 서비스 내부 미구현 지점에서 막히는 것까지 확인되는 것)
6. 나중에 추론 서비스의 TODO(`roi.ROI`, `find_board_corners`, `detect_parts`)가 채워지고 `200`이 오면, `InferenceTestController`가 그 `parts` 배열을 그대로 돌려주는 것까지 확인하면 왕복 테스트 끝

### 다음 할 일

- 추론 서비스 TODO 채워지면, 실제 `200` 응답으로 왕복 테스트 다시 해보기
- 이 테스트용 엔드포인트(`/api/inference-test`)를 실제 검사 흐름(`POST /api/trigger`)에 연결 — 지금은 별도 테스트용으로만 존재
