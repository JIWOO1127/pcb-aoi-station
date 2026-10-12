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

- ~~추론 서비스 TODO 채워지면, 실제 `200` 응답으로 왕복 테스트 다시 해보기~~ → **완료**, 아래 "왕복 연동 테스트 재확인" 참고
- 이 테스트용 엔드포인트(`/api/inference-test`)를 실제 검사 흐름(`POST /api/trigger`)에 연결 — 지금은 별도 테스트용으로만 존재

## 2026-09-28 — 왕복 연동 테스트 재확인 (추론 서비스 TODO 완료 후)

- 추론 서비스 쪽 `roi.ROI`, `find_board_corners`, `detect_parts`(YOLO)가 모두 채워진 뒤 처음으로 다시 왕복 테스트 진행
- 추론 서비스(`uvicorn main:app --port 8001`) + 스테이션(`dotnet run --urls http://localhost:5215`) 둘 다 새로 기동
  - **주의**: 포트 8001에 9월 23일부터 떠 있던 이전 세션의 uvicorn 프로세스가 이미 떠 있어서(오래된 코드로 응답), 새로 띄운 프로세스가 바인딩에 실패하고 조용히 무시되는 문제가 있었음 → `netstat -ano`로 점유 PID 확인 후 종료(`taskkill /F /PID`)하고 재기동해서 해결. 다음에도 "분명 코드 고쳤는데 반영 안 됨/500"이면 해당 포트에 오래된 프로세스가 남아있는지부터 의심할 것
- `curl -X POST http://localhost:5215/api/inference-test -F "image=@...jpg;type=image/jpeg"`로 확인:
  - `new_test_images/board4.jpg`, `test_images/board1.jpg`, `new_test_images/board2.jpg` 세 장 모두 `200` 응답, `station`이 추론 서비스의 `parts` 배열을 그대로 전달하는 것 확인 (예: board4 → `ic_chip`/`led`/`resistance330`/`resistance470`/`black_wire`/`white_wire`/`red_wire`/`capacitor` 핀홀별 결과)
  - `/infer`를 추론 서비스에 직접 호출한 결과와 `/api/inference-test`를 통해 받은 결과가 동일함도 확인 — 스테이션 쪽 프록시 로직에 변형이 없음
- **왕복 테스트 자체는 끝남.** 다음 남은 일은 위 "다음 할 일"의 두 번째 항목(`/api/inference-test`를 실제 `POST /api/trigger` 흐름에 연결)뿐
