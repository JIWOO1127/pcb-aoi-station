"""기준(정상) 보드 사진 → 레시피 초안 (POST /recipe-draft).

/infer와 같은 파이프라인(크롭·방향 판단·홀 계산·YOLO·중복 박스 제거·다리 배정)을 돌린 뒤,
YOLO 박스 하나 = 부품 하나로 묶어 "부품별 다리 홀" 초안을 만든다.
초안은 사람이 확인·수정한 뒤 MES에서 승인한다 — 그래서 의심스러운 부품에 reasons를 붙인다.

여기 함수들은 모델·이미지 없이 테스트할 수 있게 순수 계산만 한다 (main.py가 엔드포인트).
"""

from dataclasses import dataclass, field

from board import BoxGroup, Hole

# 스테이션 판정과 같은 신뢰도 기준 (Station InspectionService.MinConfidence)
REVIEW_CONFIDENCE = 0.5

# 부품 종류 → 부품 ID 접두어
_PREFIX = {
    "ic_chip": "U",
    "resistance330": "R",
    "resistance470": "R",
    "capacitor": "C",
    "led": "LED",
    "black_wire": "W",
    "red_wire": "W",
    "white_wire": "W",
}

# 부품 종류 → 정상 다리 수 (IC 14, 나머지 2)
_EXPECTED_LEGS = {"ic_chip": 14}
_DEFAULT_LEGS = 2


def prefix_of(part_class: str) -> str:
    """ic_chip→U, resistance*→R, capacitor→C, led→LED, *_wire→W. 모르는 종류는 X."""
    if part_class in _PREFIX:
        return _PREFIX[part_class]
    if part_class.startswith("resistance"):
        return "R"
    if part_class.endswith("_wire"):
        return "W"
    return "X"


def expected_legs(part_class: str) -> int:
    return _EXPECTED_LEGS.get(part_class, _DEFAULT_LEGS)


def hole_sort_key(hole: Hole) -> tuple[int, int]:
    """행(A→U) 먼저, 같으면 열(0→13)."""
    return hole.row, hole.col


@dataclass
class DraftPart:
    part_id: str
    part_class: str
    holes: list[str]
    confidence: float
    reasons: list[str]
    box: dict[str, int]

    @property
    def needs_review(self) -> bool:
        return bool(self.reasons)

    def to_json(self) -> dict:
        return {
            "partId": self.part_id,
            "class": self.part_class,
            "holes": self.holes,
            "confidence": round(float(self.confidence), 4),
            "needsReview": self.needs_review,
            "reasons": self.reasons,
            "box": self.box,
        }


@dataclass
class Draft:
    parts: list[DraftPart]
    warnings: list[str] = field(default_factory=list)


def box_to_xywh(box) -> dict[str, int]:
    """DetectedBox(x1,y1,x2,y2 실수) → {x, y, w, h} 정수 px (정렬된 사진 기준, 왼쪽 위 원점)."""
    x = int(round(box.x1))
    y = int(round(box.y1))
    return {"x": x, "y": y, "w": int(round(box.x2)) - x, "h": int(round(box.y2)) - y}


def build_draft(groups: list[BoxGroup]) -> Draft:
    """박스별 다리 묶음 → 부품 번호·검토 이유가 붙은 초안.

    번호: 접두어마다 "가장 위 홀의 행(A→U), 같으면 가장 왼쪽 열(0→13)" 순으로 1부터.
          다리를 하나도 못 받은 박스는 그 접두어의 맨 뒤 (박스 위치 위→아래, 왼→오 순).
    reasons:
      LOW_CONFIDENCE  박스 신뢰도 < 0.5 (스테이션은 판정에서 이 탐지를 뺀다)
      HOLE_COUNT      다리 수가 IC 14·나머지 2가 아님
      HOLE_OVERLAP    다리 자리가 다른 부품과 겹침 (다른 박스가 먼저 가져간 다리 후보가 있음 — 양쪽 다 표시)
    """
    # 다른 부품과 겹치는 부품 찾기: 내 다리 후보를 다른 박스가 가져갔으면 나와 그 박스 둘 다
    owner: dict[str, int] = {}
    for i, g in enumerate(groups):
        for h in g.holes:
            owner[h.label] = i
    overlap = set()
    for i, g in enumerate(groups):
        for h in g.contested:
            j = owner.get(h.label)
            if j is not None and j != i:
                overlap.update((i, j))

    def order_key(item):
        i, g = item
        if g.holes:
            top = min(g.holes, key=hole_sort_key)
            return (0, top.row, min(h.col for h in g.holes if h.row == top.row), i)
        return (1, g.box.y1, g.box.x1, i)

    by_prefix: dict[str, list[tuple[int, BoxGroup]]] = {}
    for i, g in enumerate(groups):
        by_prefix.setdefault(prefix_of(g.box.part_class), []).append((i, g))

    parts: list[DraftPart] = []
    for prefix in ("U", "R", "C", "LED", "W", "X"):
        for n, (i, g) in enumerate(sorted(by_prefix.get(prefix, []), key=order_key), start=1):
            reasons = []
            if g.box.confidence < REVIEW_CONFIDENCE:
                reasons.append("LOW_CONFIDENCE")
            if len(g.holes) != expected_legs(g.box.part_class):
                reasons.append("HOLE_COUNT")
            if i in overlap:
                reasons.append("HOLE_OVERLAP")
            parts.append(DraftPart(
                part_id=f"{prefix}{n}",
                part_class=g.box.part_class,
                holes=[h.label for h in sorted(g.holes, key=hole_sort_key)],
                confidence=g.box.confidence,
                reasons=reasons,
                box=box_to_xywh(g.box),
            ))

    warnings = []
    if not parts:
        warnings.append("부품을 하나도 찾지 못했습니다.")
    review = [p.part_id for p in parts if p.needs_review]
    if review:
        warnings.append(f"확인이 필요한 부품 {len(review)}개: {', '.join(review)}")
    return Draft(parts=parts, warnings=warnings)


def overlay_holes(holes: list[Hole]) -> list[dict]:
    """정렬된 사진 위 홀 282개의 위치 {label, x, y} (px, 소수 1자리)."""
    return [{"label": h.label, "x": round(float(h.pixel[0]), 1), "y": round(float(h.pixel[1]), 1)}
            for h in sorted(holes, key=hole_sort_key)]
