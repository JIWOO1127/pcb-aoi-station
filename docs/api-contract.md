# API 계약서

## 관리자 웹/스테이션 → MES : `POST /api/auth/login`

작업자 사번과 PIN을 검증하고 1시간 동안 사용할 수 있는 HS256 JWT를 발급한다. PIN은 DB에 BCrypt 해시로만 저장한다.

### 요청

```json
{
  "employeeId": "A-1042",
  "pin": "1234"
}
```

### 성공 응답 (`200 OK`)

```json
{
  "accessToken": "eyJ...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "employeeId": "A-1042",
    "name": "홍길동",
    "role": "WORKER"
  }
}
```

- 이후 요청은 `Authorization: Bearer {accessToken}` 헤더를 사용한다.
- 사번이 없거나, 비활성 계정이거나, PIN이 틀리면 동일하게 `401 Unauthorized`를 반환한다.
- 운영 환경에서는 MES와 스테이션에 동일한 `JWT_SECRET`(32바이트 이상)을 안전하게 설정한다.
- 계정이 비활성화되거나 Role이 변경되면 기존 JWT도 다음 요청부터 사용할 수 없다.

## 관리자 웹 → MES : 사용자 관리

모든 사용자 관리 API는 `ADMIN` JWT가 필요하다.

| Method | Path | 설명 |
|---|---|---|
| `GET` | `/api/admin/users` | 관리자 웹 사용자 목록과 최근 로그인 시각 조회 |
| `POST` | `/api/admin/users` | 사용자 생성 |
| `PUT` | `/api/admin/users/{employeeId}` | 이름, Role, 활성 상태 변경 |
| `POST` | `/api/admin/users/{employeeId}/reset-password` | PIN 초기화 (`204 No Content`) |

- Role은 `ADMIN`, `ENGINEER`, `VIEWER` 중 하나다.
- PIN은 4~64자로 받고 BCrypt 해시만 저장한다.
- 현재 로그인한 관리자는 자신의 ADMIN 권한을 해제하거나 계정을 비활성화할 수 없다.
- 마지막 활성 관리자도 비활성화하거나 다른 Role로 변경할 수 없다.
- 생성, 수정, PIN 초기화는 감사 로그에 기록하며 PIN 원문은 기록하지 않는다.

## 관리자 웹 → MES : 감사 로그

`GET /api/admin/audit-logs`는 `ADMIN` JWT가 필요하며 `actorId`, `action`, `from`, `to`, `page`, `size`로 필터링할 수 있다.

## 스테이션 → 추론 서비스 : `POST /infer`

### 요청

- Content-Type: `multipart/form-data`
- 필드: `image` (촬영·정렬 완료된 이미지 파일, **JPEG만** — `image/jpeg`)

### 응답

```json
{
  "parts": [
    {
      "pinhole": "J9",
      "class": "resistor_220",
      "confidence": 0.97
    }
  ]
}
```

| 필드 | 설명 |
|---|---|
| `pinhole` | 부품이 꽂힌 핀홀 라벨. "행글자 + 열숫자" (예: `"J9"`). 행 A~U(위→아래), 열 0~13(왼쪽→오른쪽). 그리드 정의는 [inference/app/pinhole_grid.py](../inference/app/pinhole_grid.py) 참고 |
| `class` | 탐지된 부품 종류 |
| `confidence` | 탐지 신뢰도 (0~1) |

**주의 (README 기존 설계와의 차이)**
- README 초안(§2 검사 흐름)에는 추론 서비스가 "부품별 종류·신뢰도·**박스**(픽셀 좌표)"를 반환하고, 스테이션이 이를 레시피(`x_mm`, `y_mm`)와 비교해 판정하는 것으로 되어 있었음.
- 이번 결정으로 **픽셀 박스 대신 브레드보드 핀홀 라벨을 추론 서비스가 직접 계산해서 반환**하는 것으로 바뀜 → 즉 "픽셀 좌표 → 핀홀 라벨" 변환 로직이 스테이션이 아니라 추론 서비스 쪽에 위치하게 됨.
- 판정(정상/누락/오장착 여부 등) 자체는 여전히 스테이션이 한다는 원칙은 유지. 추론 서비스는 "어떤 부품이 어느 핀홀에 있는지"까지만 알려준다.

---

## 스테이션 → MES: 검사 기록 API (MVP-25) — 구현, 2026-09-28

작성 서지우(스테이션) · 검토 이하연(MES). 기준: main의 MES 스키마(마이그레이션 V1~V3)와 인증 API.
스테이션 쪽 구현 예정: 검사 완료 후 전송하고, 실패 시 로컬 보관 후 재전송.

### 요약


- 스테이션은 검사가 끝날 때마다 `POST /api/inspections`로 기록 한 건을 보낸다.
- MES가 꺼져 있으면 스테이션이 PC에 쌓아뒀다가 연결되면 **오래된 것부터 다시** 보낸다.
- 그래서 **같은 `inspectionId`가 두 번 이상 올 수 있다.** MES는 두 번째부터 저장하지 않고 `409`를 돌려주면 된다. 스테이션은 `201`과 `409`를 모두 성공으로 본다.
- MES는 이미지와 이미지 경로를 저장하지 않는다.

### 1. 요청

```
POST /api/inspections
Content-Type: application/json
X-Station-Key: <스테이션 키>
```

#### 인증: 작업자 JWT가 아니라 스테이션 키
- 작업자 토큰은 1시간이면 만료된다. MES가 끊긴 동안 쌓인 기록을 몇 시간 뒤 보내면 401로 거절된다.
- 그래서 이 API만 **스테이션 키 헤더(`X-Station-Key`)** 로 확인한다. 키는 양쪽 설정 파일에 같은 값을 넣는다.
- 작업자는 본문의 `workerId`(HMI 로그인 사번)로 기록한다.
- 구현 제안: JWT 필터에서 아래 표의 **POST 두 개만** 예외로 두고, 헤더 값이 설정값(`STATION_KEY`)과 같은지 확인. 다르면 401.

**인증 범위** (검사 기록 API)

| API | 호출하는 쪽 | 인증 |
|---|---|---|
| `POST /api/inspections` (검사 기록) | 스테이션 | `X-Station-Key` |
| `POST /api/inspections/{inspectionId}/confirmations` (확인 기록) | 스테이션 | `X-Station-Key` |
| `GET /api/inspections`, `GET /api/inspections/{id}` (조회) | 관리자 웹 | JWT (`Authorization: Bearer {accessToken}`) |

#### 본문

```json
{
  "inspectionId": "3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90",
  "recipeId": "7bafaa9d-4d9f-4d2e-bc04-0db47d401003",
  "stationId": "ST-01",
  "productId": "NE555-B",
  "workerId": "A-1042",
  "verdict": "FAIL",
  "defectCount": 1,
  "errorCode": null,
  "latencyMs": 1730,
  "capturedAt": "2026-09-29T14:03:22.418+09:00",
  "parts": [
    {
      "partId": "W4",
      "expectedClass": "black_wire",
      "detectedClass": "black_wire",
      "defectType": "OFFSET",
      "expectedHoles": ["H11", "I11", "J11", "K11", "L11", "M11"],
      "coveredHoles": ["H11", "I11", "J11", "K11", "L11"]
    },
    {
      "partId": "C1",
      "expectedClass": "capacitor",
      "detectedClass": "capacitor",
      "defectType": null,
      "expectedHoles": ["Q9", "Q10"],
      "coveredHoles": ["Q9", "Q10"]
    }
  ]
}
```

| 필드 | 타입 | 필수 | 설명 |
|---|---|---|---|
| inspectionId | UUID | O | 스테이션이 만든 ID. **중복 판단 기준** |
| recipeId | UUID | O | 검사에 사용한 레시피 ID. productId와 일치해야 함 |
| stationId | string | O | 스테이션 이름 (`ST-01`) |
| productId | string | O | `product` 테이블에 있어야 함 (`NE555-B`) |
| workerId | string \| null | - | HMI 로그인 사번. **NULL 허용**: 로그인 없이 검사한 경우 null (`inspection.worker_id`도 NULL 허용) |
| verdict | string | O | `PASS` \| `FAIL` \| `INVALID` (판정 불가) |
| defectCount | int | O | 불량 부품 수 |
| errorCode | string | - | `INVALID`일 때 이유: `BOARD_NOT_RECOGNIZED`, `INFERENCE_TIMEOUT`, `INFERENCE_UNAVAILABLE` |
| latencyMs | int | O | 검사 처리 시간 |
| capturedAt | ISO-8601 (+09:00) | O | 검사 시각 |
| parts[].partId | string | O | 레시피 부품 ID. 추가 부품은 `EXTRA@B5` |
| parts[].expectedClass | string | O | 있어야 할 종류. 추가 부품은 `-` |
| parts[].detectedClass | string | - | 실제로 본 종류. 비었으면 null |
| parts[].defectType | string | - | null(정상) \| `MISSING` \| `WRONG_PART` \| `WIRE_COLOR_SWAP` \| `OFFSET` \| `EXTRA` |
| parts[].expectedHoles | string[] | O | 레시피 홀. 배열 형태(예: `["E9","H9"]`), JSONB 배열로 저장 |
| parts[].coveredHoles | string[] | O | 실제로 덮인 홀. 배열 형태(예: `["E9","H9"]`), JSONB 배열로 저장 |

`INVALID`이면 `parts`는 빈 배열이다.

### 2. 응답

| 상황 | 코드 | 본문 | 스테이션 동작 |
|---|---|---|---|
| 새로 저장 | 201 | `{"inspectionId":"..."}` | 보관함에서 삭제 |
| 같은 inspectionId가 이미 있음 | 409 | 아무거나 | **성공으로 보고** 삭제 |
| 스테이션 키 틀림 | 401 | 에러 | 따로 빼 둠 (사람이 확인) |
| 본문 오류 (필수값 누락, 없는 productId 등) | 400 / 422 | `{"message":"..."}` | 따로 빼 둠 (사람이 확인) |
| MES 내부 오류 | 5xx | - | 잠시 뒤 재시도 |

### 3. 스키마 변경 (V4 적용)

`V4__remove_inspection_image_path.sql`에서 아래 변경을 적용한다.

| 문제 | 제안 |
|---|---|
| `inspection.verdict` CHECK가 `PASS`, `FAIL`만 허용 | `INVALID` 추가 (판정 불가도 추적 대상) |
| `errorCode`를 받을 곳 없음 | `inspection.error_code VARCHAR(50) NULL` 추가 |
| `stationId`를 받을 곳 없음 | `inspection.station_id VARCHAR(50) NULL` 추가 |
| `inspection.worker_id` NOT NULL, HMI 로그인 없이 검사하면 사번 없음 | **NULL 허용으로 변경** (결정) |
| `part_result`에 홀 정보 없음 (판정이 mm 좌표 → 핀홀 방식으로 바뀜) | `expected_class VARCHAR(100)`, `expected_holes`·`covered_holes` 추가 — 배열 형태(예: `["E9","H9"]`), JSONB 배열로 저장 (MES 담당 제안) |
| `recipe_part`가 mm 좌표 기준 (`x_mm`, `y_mm`, `tolerance_mm`) | `holes` 추가 (레시피 홀 목록) — 배열 형태(예: `["E9","H9"]`), JSONB 배열로 저장. mm 컬럼은 정리 |
| `part_result.offset_mm`, `confidence` | 사용 안 함, NULL로 둠 |
| `part_result_id` | MES가 생성 (`gen_random_uuid()` 또는 JPA에서 UUID) |
| `product`에 `NE555-B` 필요 (FK) | 시드 데이터로 추가 |
| 시드 작업자 없음 | 시연용 작업자 1~2명 시드 (PIN은 BCrypt 해시) |

### 4. 조회 API (Web 연동)

- `GET /api/inspections?verdict=&productId=&stationId=` → 최근 검사 목록
- `GET /api/inspections/{id}` → 부품별 결과 포함
- 인증은 JWT (`Authorization: Bearer`) — 위 1번의 인증 범위 표 참고. 응답 모양은 아래 "후속 작업"에서 정한다.

### 5. 확인 기록 API (불량 확정) — 초안

HMI에서 FAIL 보드를 **불량 확정(F)** 하면 스테이션이 MES로 보낸다. 확인 기록은 덮어쓰지 않고 추가만 한다(README 원칙).
수리 후 재검사는 확인 기록이 아니라 **새 검사 기록**으로 남는다 (새 inspectionId).

```
POST /api/inspections/{inspectionId}/confirmations
Content-Type: application/json
X-Station-Key: <스테이션 키>
```
```json
{
  "confirmationId": "9b1d2c3e-1111-4a2b-8c3d-000000000001",
  "workerId": "A-1042",
  "decision": "REJECTED",
  "reasonCode": null,
  "confirmedAt": "2026-09-29T14:05:10.000+09:00"
}
```
| 필드 | 설명 |
|---|---|
| confirmationId | 스테이션이 생성. 재전송 시 중복 판단 기준 (같은 ID면 409) |
| decision | `REJECTED`(불량 확정). 이후 필요하면 `ACCEPTED`(작업자가 양품으로 판단) 등 추가 |
| reasonCode | 선택. 예: `REPAIR_IMPOSSIBLE` |

응답은 검사 기록과 같다 (201 / 409 / 400·422 / 5xx). `{inspectionId}`가 아직 MES에 없으면(검사 기록이 보관함에서 대기 중) `404` → 스테이션은 잠시 뒤 재시도한다. 스테이션은 검사 기록을 먼저, 확인 기록을 나중에 보내므로 순서가 맞으면 404는 거의 생기지 않는다.

> 구현 상태: 스테이션·HMI 쪽 전송은 아직 없음. 이 문서의 요청 형식에 맞춰 스테이션 담당자가 구현한다.

### 6. 합의할 것 (월요일 아침)

- [x] `workerId` null 처리 → **NULL 허용** (로그인 없이 검사한 경우)
- [x] 홀 목록 저장 형식 → 배열 형태(예: `["E9","H9"]`), JSONB 배열로 저장 (MES 담당 제안)
- [ ] 스테이션 키 값과 설정 이름
- [ ] 시연 작업자 사번·PIN
- [ ] MES를 띄울 노트북(LG)과 주소

### 7. 후속 작업

- **레시피 홀 기준 전환**: 박스가 덮은 홀 → 부품 다리가 꽂힌 홀 (inference MVP-32, 레시피 NE555-A v4 / NE555-B v2).
  전환 후 `expectedHoles`·`coveredHoles`는 다리 홀만 담는다 (예: 전선·저항 2개, IC 14개). 위 본문 예시는 전환 전 형식이다.
- **웹 조회 응답 계약**: `GET /api/inspections`, `GET /api/inspections/{id}`의 응답 JSON 모양 (목록 필드, 페이지 처리, 부품별 결과 포함 범위).
- **스테이션·HMI 전송 연동**: 검사 기록과 확인 기록 API 호출, 연결 실패 시 로컬 보관 후 재전송을 스테이션 담당자가 구현한다.
