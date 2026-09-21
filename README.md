# PCB AOI 검사 스테이션

브레드보드 PCB의 부품 오장착(누락·위치·오부품·전선 색)을 카메라로 검사하고, 결과를 MES에 기록하는 시스템.

## 1. 시스템 구성도

```mermaid
flowchart LR
    CAM[카메라<br/>iPhone + Camo]

    subgraph VPC[비전 PC · 라인 단]
        HMI[작업자 HMI<br/>C# WPF]
        ST[스테이션 서버<br/>C# ASP.NET Core<br/>판정 규칙 엔진]
        INF[추론 서비스<br/>Python · YOLO]
        BUF[(SQLite<br/>단절 시 버퍼)]
    end

    subgraph SRV[MES 서버 · 관리 단]
        MES[MES 서버<br/>Java Spring Boot]
        DB[(PostgreSQL)]
    end

    WEB[관리자 웹<br/>React]

    CAM -->|영상| ST
    HMI -->|검사 트리거·재검사| ST
    ST -->|결과| HMI
    ST -->|이미지| INF
    INF -->|부품 탐지 결과| ST
    ST <--> BUF
    ST -->|검사 기록 전송| MES
    MES -->|레시피·작업자 정보| ST
    MES <--> DB
    WEB -->|조회·기준 관리| MES
```

**원칙**
- 판정은 스테이션(C#) 한 곳에서만 한다. 추론 서비스는 탐지 결과만, MES는 기록과 기준만 담당한다.
- DB에 직접 붙는 것은 MES뿐이다. 나머지는 모두 API로 접근한다.
- MES가 끊겨도 검사는 계속된다. 스테이션이 SQLite에 쌓아두고 복구 후 재전송한다.

## 2. 검사 흐름

```mermaid
sequenceDiagram
    actor 작업자
    participant HMI as HMI (WPF)
    participant ST as 스테이션 (C#)
    participant INF as 추론 (Python)
    participant MES as MES (Java)

    작업자->>HMI: Space (검사)
    HMI->>ST: POST /api/trigger
    ST->>ST: 프레임 획득 · 촬영 품질 검사 · 기준 마크 정렬
    ST->>INF: POST /infer (정렬된 이미지)
    INF-->>ST: 부품별 종류 · 신뢰도 · 박스
    ST->>ST: 레시피와 비교해 판정 (누락·위치·오부품·전선 색)
    ST-->>HMI: 판정 결과
    ST->>MES: POST /inspections (실패 시 SQLite 보관 후 재전송)
    작업자->>HMI: FAIL 확인 (불량품 분리)
    HMI->>ST: 확인 기록
    ST->>MES: POST /inspections/{id}/confirmations
```

## 3. 저장소 구조

```
pcb-aoi-station/
├── README.md
├── docs/                         설계 문서
│   ├── api-contract.md           API 계약서
│   └── defect-criteria.md        불량 기준서 (한도 견본 포함)
├── station/                      C# · 조장
│   ├── Inspection.slnx
│   ├── Inspection.Contracts/     HMI와 스테이션이 공유하는 데이터 모델
│   ├── Inspection.Hmi/           작업자 화면 (WPF)
│   └── Inspection.Station/       스테이션 서버 (ASP.NET Core)
├── inference/                    Python · 비전 담당
│   ├── app/                      FastAPI 추론 서비스
│   ├── training/                 학습 스크립트
│   └── requirements.txt
├── mes/                          Java · 백엔드 담당
│   ├── src/main/java/...         Spring Boot
│   ├── src/main/resources/db/    DB 마이그레이션 SQL
│   └── build.gradle
└── web/                          React · 관리자 웹
```

모델 가중치 파일과 촬영 데이터셋은 용량이 커서 Git에 올리지 않는다 (`.gitignore`). 공유 드라이브에 두고 경로만 문서에 적는다.

## 4. 모듈별 정보

| 모듈 | 폴더 | 기술 | 포트 | 담당 |
|---|---|---|---|---|
| 작업자 HMI | `station/Inspection.Hmi` | C# WPF (.NET 10) | — | |
| 스테이션 서버 | `station/Inspection.Station` | C# ASP.NET Core (.NET 10) | 5215 | |
| 추론 서비스 | `inference` | Python, FastAPI, YOLO | 8001 | |
| MES 서버 | `mes` | Java, Spring Boot | 8080 | |
| DB | — | PostgreSQL | 5432 | |
| 관리자 웹 | `web` | React | 5173 | |

## 5. DB 주요 테이블 (MES 소유)

```mermaid
erDiagram
    PRODUCT ||--o{ RECIPE_PART : "부품 구성"
    PRODUCT ||--o{ INSPECTION : "검사 대상"
    WORKER ||--o{ INSPECTION : "검사 수행"
    INSPECTION ||--o{ PART_RESULT : "부품별 결과"
    INSPECTION ||--o{ CONFIRMATION : "작업자 확인"

    PRODUCT {
        string product_id PK
        string name
    }
    RECIPE_PART {
        string product_id FK
        string part_id
        string expected_class
        float x_mm
        float y_mm
        float tolerance_mm
        string check_mode
    }
    WORKER {
        string worker_id PK
        string name
        string pin_hash
    }
    INSPECTION {
        uuid inspection_id PK
        string product_id FK
        string worker_id FK
        string verdict
        int defect_count
        string image_path
        int latency_ms
        timestamp captured_at
    }
    PART_RESULT {
        uuid inspection_id FK
        string part_id
        string detected_class
        float confidence
        float offset_mm
        string defect_type
    }
    CONFIRMATION {
        uuid confirmation_id PK
        uuid inspection_id FK
        string worker_id FK
        string decision
        string reason_code
        timestamp confirmed_at
    }
```

- `inspection_id`는 스테이션이 생성한다. 같은 ID가 재전송되면 MES는 중복 저장하지 않는다.
- 확인 기록은 덮어쓰지 않고 추가만 한다 (감사 추적).
- 이미지는 DB에 넣지 않고 경로만 저장한다.

## 6. 작업 규칙

**브랜치**
- `main`: 항상 실행 가능한 상태. 직접 푸시 금지.
- 작업 브랜치: `모듈/작업내용` (예: `station/hmi-layout`, `mes/inspection-api`, `inference/yolo-train`)
- 작업이 끝나면 PR을 올리고 한 명 이상 확인 후 병합한다.

**충돌 줄이기**
- 각자 자기 폴더 안에서만 작업한다.
- API나 데이터 모양을 바꿀 때는 `docs/api-contract.md`를 먼저 고치고 팀에 알린다.

**커밋 메시지**
- `[station] 검사 결과 화면 추가`, `[mes] 검사 기록 저장 API` 처럼 모듈을 앞에 붙인다.

## 7. 실행 방법

각 모듈 준비가 끝나면 채운다.
