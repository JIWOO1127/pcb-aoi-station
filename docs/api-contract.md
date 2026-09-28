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
