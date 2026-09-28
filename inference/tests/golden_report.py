"""골든(정상) 보드 사진으로 /recipe-draft 초안을 만들어 스테이션 레시피와 비교하는 보고서.

실행 (inference 폴더에서, Tesseract가 PATH에 있어야 함):
    ..\\.venv\\Scripts\\python.exe tests\\golden_report.py [사진...]
기본 사진: C:\\temp\\golden_1.jpg ~ golden_3.jpg, 비교 대상: tests/fixtures/NE555-B.v3.station.json

부품 대응: 레시피 부품마다 "홀이 가장 많이 겹치는 초안 부품"을 짝으로 본다.
  클래스 일치 = 짝의 class가 같음, 홀 일치 = 홀 집합이 완전히 같음.
주의: golden_1~3 사진 속 R1은 실제 470Ω이다 (레시피 v3는 330). R1 클래스 불일치는 예상된 결과로 따로 표시한다.
"""

import json
import sys
from pathlib import Path

APP = Path(__file__).resolve().parents[1] / "app"
sys.path.insert(0, str(APP))

from fastapi.testclient import TestClient  # noqa: E402

import main  # noqa: E402

RECIPE = Path(__file__).resolve().parent / "fixtures" / "NE555-B.v3.station.json"
EXPECTED_MISMATCH = {"R1": "사진 속 R1은 실제 470Ω (레시피 330)"}


def compare(draft: dict, recipe: dict) -> list[dict]:
    rows = []
    for rp in recipe["parts"]:
        want = set(rp["holes"])
        best = max(draft["parts"], key=lambda dp: len(want & set(dp["holes"])), default=None)
        overlap = len(want & set(best["holes"])) if best else 0
        rows.append({
            "partId": rp["partId"],
            "expectedClass": rp["expectedClass"],
            "expectedHoles": rp["holes"],
            "draftPartId": best["partId"] if best and overlap else "-",
            "draftClass": best["class"] if best and overlap else "-",
            "draftHoles": best["holes"] if best and overlap else [],
            "classMatch": bool(best and overlap and best["class"] == rp["expectedClass"]),
            "holeMatch": bool(best and set(best["holes"]) == want),
            "reasons": best["reasons"] if best and overlap else [],
            "note": EXPECTED_MISMATCH.get(rp["partId"], ""),
        })
    return rows


def main_report(paths: list[Path]) -> None:
    recipe = json.loads(RECIPE.read_text(encoding="utf-8-sig"))
    client = TestClient(main.app)
    for path in paths:
        r = client.post("/recipe-draft", files={"image": (path.name, path.read_bytes(), "image/jpeg")},
                        data={"productId": recipe["productId"]})
        print(f"\n=== {path.name} → HTTP {r.status_code}")
        if r.status_code != 200:
            print("   ", r.json())
            continue
        draft = r.json()
        rows = compare(draft, recipe)
        print("부품 | 레시피 class | 초안 class | 클래스 | 레시피 홀 | 초안 홀 | 홀 | reasons | 비고")
        for x in rows:
            print(f"{x['partId']} | {x['expectedClass']} | {x['draftClass']} ({x['draftPartId']}) | "
                  f"{'O' if x['classMatch'] else 'X'} | {','.join(x['expectedHoles'])} | {','.join(x['draftHoles']) or '-'} | "
                  f"{'O' if x['holeMatch'] else 'X'} | {','.join(x['reasons']) or '-'} | {x['note']}")
        n = len(rows)
        cls = sum(x["classMatch"] for x in rows)
        hol = sum(x["holeMatch"] for x in rows)
        both = sum(x["classMatch"] and x["holeMatch"] for x in rows)
        cls_ex = sum(x["classMatch"] or bool(x["note"]) for x in rows)
        matched_ids = {x["draftPartId"] for x in rows}
        extra = [f"{p['partId']}({p['class']})" for p in draft["parts"] if p["partId"] not in matched_ids]
        print(f"일치율: 클래스 {cls}/{n} ({cls_ex}/{n} R1 예상 불일치 제외), 홀 {hol}/{n}, 둘 다 {both}/{n} | "
              f"초안 부품 {len(draft['parts'])}개, 레시피에 없는 초안 부품: {extra or '없음'} | warnings: {draft['warnings']}")


if __name__ == "__main__":
    args = [Path(a) for a in sys.argv[1:]] or [Path(rf"C:\temp\golden_{i}.jpg") for i in (1, 2, 3)]
    main_report(args)
