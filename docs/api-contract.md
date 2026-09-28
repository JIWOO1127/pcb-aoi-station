# API 계약서

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
      "class": "resistance330",
      "confidence": 0.97
    }
  ]
}
```

| 필드 | 설명 |
|---|---|
| `pinhole` | 부품 **다리 하나**가 꽂힌 핀홀 라벨. "행글자 + 열숫자" (예: `"J9"`). 행 A~U(위→아래), 열 0~13(왼쪽→오른쪽). 그리드 정의는 [inference/app/pinhole_grid.py](../inference/app/pinhole_grid.py) 참고. 부품 하나가 다리를 여러 개 쓰면(예: IC칩 14개) 그 개수만큼 배열 원소가 나뉘어 나온다 — 아래 "다리 개수만큼 나온다" 참고 |
| `class` | 탐지된 부품 종류. 지금 학습된 8종: `black_wire`, `capacitor`, `ic_chip`, `led`, `red_wire`, `resistance330`, `resistance470`, `white_wire` |
| `confidence` | 탐지 신뢰도 (0~1) |

**부품당 다리 개수만큼 나온다**
- `parts` 배열의 한 원소 = 다리 하나가 꽂힌 핀홀 하나. 부품 하나가 다리를 여러 개 쓰면 그만큼 원소가 여러 개 나온다 (`pinhole`만 다르고 `class`·`confidence`는 같은 부품이면 동일).
- 다리 개수는 부품 종류별로 고정: `led`/`capacitor` 2개, `resistance330`/`resistance470`/`black_wire`/`red_wire`/`white_wire` 2개(몸통 양 끝), `ic_chip` 14개(몸통 양옆 두 줄).
- 몸통이 핀홀 여러 개 위에 걸쳐 보여도(예: IC칩 밑에 안 쓰는 가운데 두 열) 실제 다리가 없는 핀홀은 배열에 안 나온다 — `inference/app/board.py`의 `match_holes_to_boxes` 참고.

**주의 (README 기존 설계와의 차이)**
- README 초안(§2 검사 흐름)에는 추론 서비스가 "부품별 종류·신뢰도·**박스**(픽셀 좌표)"를 반환하고, 스테이션이 이를 레시피(`x_mm`, `y_mm`)와 비교해 판정하는 것으로 되어 있었음.
- 이번 결정으로 **픽셀 박스 대신 브레드보드 핀홀 라벨을 추론 서비스가 직접 계산해서 반환**하는 것으로 바뀜 → 즉 "픽셀 좌표 → 핀홀 라벨" 변환 로직이 스테이션이 아니라 추론 서비스 쪽에 위치하게 됨.
- 판정(정상/누락/오장착 여부 등) 자체는 여전히 스테이션이 한다는 원칙은 유지. 추론 서비스는 "어떤 부품이 어느 핀홀에 있는지"까지만 알려준다.
