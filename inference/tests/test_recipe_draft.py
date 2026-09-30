"""POST /recipe-draft 테스트 (표준 unittest — 저장소 .venv에 pytest가 없어서).

실행 (inference 폴더에서):
    ..\\.venv\\Scripts\\python.exe -m unittest discover -s tests -v

- 가짜 탐지 결과: 부품 번호 규칙, reasons 3종, 박스 좌표, 응답 구조, 오류 코드
- 가중치(app/models/yolo8n.pt)와 C:\\temp\\test1.jpg가 있으면: /infer 응답이 변경 전 기준과 완전히 같은지 (회귀)
- C:\\temp\\recipe-draft-sample.json(합의한 명세 샘플)이 있으면: 응답 키·타입 구조가 샘플과 같은지
"""

import hashlib
import io
import json
import sys
import unittest
from pathlib import Path
from unittest import mock

APP = Path(__file__).resolve().parents[1] / "app"
sys.path.insert(0, str(APP))

import cv2  # noqa: E402
import numpy as np  # noqa: E402
from fastapi.testclient import TestClient  # noqa: E402

import main  # noqa: E402
import recipe_draft  # noqa: E402
from board import BoxGroup, DetectedBox, Hole, match_holes_to_boxes, match_holes_to_boxes_grouped  # noqa: E402
from pinhole_grid import ROWS  # noqa: E402

FIXTURES = Path(__file__).resolve().parent / "fixtures"
SAMPLE = Path(r"C:\temp\recipe-draft-sample.json")
TEST1 = Path(r"C:\temp\test1.jpg")
WEIGHTS = APP / "models" / "yolo8n.pt"


def hole(label: str, x: float = 0, y: float = 0) -> Hole:
    return Hole(label=label, row=ROWS.index(label[0]), col=int(label[1:]), pixel=(x, y))


def box(cls: str, conf: float = 0.9, x1=0.0, y1=0.0, x2=10.0, y2=10.0) -> DetectedBox:
    return DetectedBox(x1=x1, y1=y1, x2=x2, y2=y2, part_class=cls, confidence=conf)


def group(cls: str, labels: list[str], conf: float = 0.9, contested: list[str] | None = None, **b) -> BoxGroup:
    return BoxGroup(box=box(cls, conf, **b), holes=[hole(h) for h in labels],
                    contested=[hole(h) for h in (contested or [])])


def jpeg_bytes(w=64, h=64) -> bytes:
    return cv2.imencode(".jpg", np.full((h, w, 3), 200, np.uint8))[1].tobytes()


class PartNumberingTests(unittest.TestCase):
    def test_접두어별로_가장_위_행_같으면_왼쪽_열_순서(self):
        draft = recipe_draft.build_draft([
            group("black_wire", ["J10", "J13"]),
            group("red_wire", ["H0", "H2"]),
            group("resistance330", ["N3", "Q3"]),
            group("resistance470", ["E9", "H9"]),
            group("white_wire", ["H5", "M5"]),       # H행, 5열 → H0 다음
            group("ic_chip", [f"{r}{c}" for r in "HIJKLMN" for c in (6, 7)]),
            group("led", ["D7", "E7"]),
            group("capacitor", ["Q10", "R10"]),
        ])
        ids = {p.part_id: (p.part_class, p.holes[0]) for p in draft.parts}
        self.assertEqual(ids["U1"], ("ic_chip", "H6"))
        self.assertEqual(ids["R1"], ("resistance470", "E9"))      # E행이 N행보다 위
        self.assertEqual(ids["R2"], ("resistance330", "N3"))
        self.assertEqual(ids["C1"][0], "capacitor")
        self.assertEqual(ids["LED1"][0], "led")
        self.assertEqual([ids[f"W{i}"][1] for i in (1, 2, 3)], ["H0", "H5", "J10"])
        # 출력 순서: U, R, C, LED, W
        self.assertEqual([p.part_id for p in draft.parts], ["U1", "R1", "R2", "C1", "LED1", "W1", "W2", "W3"])

    def test_홀은_행_열_순으로_정렬되고_다리_없는_박스는_접두어_맨_뒤(self):
        draft = recipe_draft.build_draft([
            group("black_wire", [], y1=5.0, x1=1.0),
            group("black_wire", ["D13", "D9"]),
        ])
        self.assertEqual([(p.part_id, p.holes) for p in draft.parts], [("W1", ["D9", "D13"]), ("W2", [])])


class ReasonTests(unittest.TestCase):
    def test_정상이면_이유_없음(self):
        p = recipe_draft.build_draft([group("led", ["D7", "E7"], conf=0.97)]).parts[0]
        self.assertEqual(p.reasons, [])
        self.assertFalse(p.needs_review)

    def test_LOW_CONFIDENCE는_0_5_미만(self):
        low, edge = recipe_draft.build_draft([
            group("led", ["D7", "E7"], conf=0.49),
            group("led", ["D2", "D3"], conf=0.50),
        ]).parts
        by_hole = {p.holes[0]: p for p in (low, edge)}
        self.assertEqual(by_hole["D7"].reasons, ["LOW_CONFIDENCE"])
        self.assertEqual(by_hole["D2"].reasons, [])

    def test_HOLE_COUNT는_IC_14_나머지_2가_아닐_때(self):
        parts = recipe_draft.build_draft([
            group("ic_chip", [f"{r}{c}" for r in "HIJKLMN" for c in (6, 7)]),          # 14 → 정상
            group("ic_chip", [f"{r}{c}" for r in "PQRST" for c in (6, 7)]),            # 10
            group("red_wire", ["N0"]),                                                  # 1
        ]).parts
        counts = {len(p.holes): p.reasons for p in parts}
        self.assertEqual(counts[14], [])
        self.assertEqual(counts[10], ["HOLE_COUNT"])
        self.assertEqual(counts[1], ["HOLE_COUNT"])

    def test_HOLE_OVERLAP은_다리_자리가_겹친_두_부품_모두(self):
        # 실제 배정 함수로: 두 전선 박스가 같은 끝 홀(D13)을 두고 겹침 → 먼저 온 박스가 가져감
        holes = [hole(f"D{c}", x=c * 10.0, y=5.0) for c in range(9, 14)]
        boxes = [box("black_wire", 0.9, x1=85, y1=0, x2=135, y2=10),     # D9~D13
                 box("black_wire", 0.8, x1=125, y1=0, x2=135, y2=10)]   # D13만
        groups = match_holes_to_boxes_grouped(holes, boxes)
        self.assertEqual([h.label for h in groups[0].holes], ["D9", "D13"])
        self.assertEqual(groups[1].holes, [])
        self.assertEqual([h.label for h in groups[1].contested], ["D13"])

        parts = {p.part_id: p for p in recipe_draft.build_draft(groups).parts}
        self.assertIn("HOLE_OVERLAP", parts["W1"].reasons)
        self.assertIn("HOLE_OVERLAP", parts["W2"].reasons)
        self.assertIn("HOLE_COUNT", parts["W2"].reasons)
        self.assertTrue(parts["W2"].needs_review)


class BoxTests(unittest.TestCase):
    def test_박스는_정수_px_x_y_w_h(self):
        self.assertEqual(recipe_draft.box_to_xywh(box("led", x1=10.4, y1=20.6, x2=50.5, y2=61.49)),
                         {"x": 10, "y": 21, "w": 40, "h": 40})
        p = recipe_draft.build_draft([group("led", ["D7", "E7"], x1=10.4, y1=20.6, x2=50.5, y2=61.49)]).parts[0]
        self.assertEqual(p.to_json()["box"], {"x": 10, "y": 21, "w": 40, "h": 40})


class GroupedMatchingTests(unittest.TestCase):
    def test_묶음_함수는_기존_함수와_홀_배정이_같다(self):
        rng = np.random.default_rng(7)
        for _ in range(30):
            holes_a = [hole(f"{r}{c}", x=c * 10 + rng.normal(0, 1), y=ROWS.index(r) * 10 + rng.normal(0, 1))
                       for r in ROWS for c in range(14)]
            holes_b = [Hole(h.label, h.row, h.col, h.pixel) for h in holes_a]
            classes = ["ic_chip", "led", "capacitor", "black_wire", "resistance330", "red_wire"]
            boxes = []
            for _ in range(8):
                x1, y1 = rng.uniform(0, 120), rng.uniform(0, 190)
                boxes.append(box(str(rng.choice(classes)), float(rng.uniform(0.3, 1)),
                                 x1=x1, y1=y1, x2=x1 + rng.uniform(5, 60), y2=y1 + rng.uniform(5, 80)))
            self.assertIsNone(match_holes_to_boxes(holes_a, boxes))
            groups = match_holes_to_boxes_grouped(holes_b, boxes)
            self.assertEqual([(h.label, h.part_class, h.confidence) for h in holes_a],
                             [(h.label, h.part_class, h.confidence) for h in holes_b])
            assigned = [h.label for g in groups for h in g.holes]
            self.assertEqual(sorted(assigned), sorted(h.label for h in holes_b if h.covered))


def _type_shape(value):
    """키·타입 구조 (값은 무시, 목록은 첫 원소 모양)."""
    if isinstance(value, dict):
        return {k: _type_shape(v) for k, v in value.items()}
    if isinstance(value, list):
        return [_type_shape(value[0])] if value else []
    if isinstance(value, bool):
        return "bool"
    if isinstance(value, int):
        return "int"
    if isinstance(value, float):
        return "number"
    return type(value).__name__


class EndpointTests(unittest.TestCase):
    """파이프라인(크롭·홀·YOLO)을 가짜로 바꿔 끼우고 엔드포인트 형식만 본다 (가중치·사진 불필요)."""

    def setUp(self):
        self.client = TestClient(main.app)
        self.roi = np.full((120, 100, 3), 180, np.uint8)
        self.holes = [hole(f"{r}{c}", x=c * 7.0 + 3, y=ROWS.index(r) * 5.5 + 2)
                      for r in ROWS for c in range(14)]
        self.boxes = [box("led", 0.97, x1=45, y1=12, x2=60, y2=31),          # D7·E7 근처
                      box("black_wire", 0.42, x1=60, y1=16, x2=98, y2=21)]   # D9~D13, 신뢰도 낮음

    def post(self, files=None, data=None):
        with mock.patch.object(main, "crop_to_roi", return_value=(self.roi, 1.0, [])), \
             mock.patch.object(main, "all_holes_from_image", return_value=(self.roi, self.holes)), \
             mock.patch.object(main, "detect_parts", return_value=self.boxes):
            return self.client.post("/recipe-draft", files=files, data=data)

    def ok_response(self):
        r = self.post(files={"image": ("golden.jpg", jpeg_bytes(), "image/jpeg")}, data={"productId": "NE555-D"})
        self.assertEqual(r.status_code, 200, r.text)
        return r.json()

    def test_응답_구조(self):
        body = self.ok_response()
        self.assertEqual(set(body), {"productId", "parts", "overlay", "warnings"})
        self.assertEqual(body["productId"], "NE555-D")
        self.assertEqual(set(body["overlay"]), {"image", "width", "height", "holes"})
        self.assertEqual((body["overlay"]["width"], body["overlay"]["height"]), (100, 120))
        self.assertEqual(len(body["overlay"]["holes"]), len(self.holes))
        self.assertEqual(set(body["overlay"]["holes"][0]), {"label", "x", "y"})
        self.assertTrue(all(isinstance(h["x"], int) and isinstance(h["y"], int) for h in body["overlay"]["holes"]))
        self.assertTrue(cv2.imdecode(np.frombuffer(__import__("base64").b64decode(body["overlay"]["image"]), np.uint8),
                                     cv2.IMREAD_COLOR) is not None)
        for p in body["parts"]:
            self.assertEqual(set(p), {"partId", "class", "holes", "confidence", "needsReview", "reasons", "box"})
            self.assertEqual(set(p["box"]), {"x", "y", "w", "h"})
            self.assertTrue(all(isinstance(v, int) for v in p["box"].values()))
            self.assertIsInstance(p["confidence"], float)
            self.assertEqual(p["needsReview"], bool(p["reasons"]))
        self.assertTrue(all(isinstance(w, str) for w in body["warnings"]))
        by_id = {p["partId"]: p for p in body["parts"]}
        self.assertEqual(by_id["LED1"]["holes"], ["D7", "E7"])
        self.assertEqual(by_id["W1"]["reasons"], ["LOW_CONFIDENCE"])

    def test_합의_샘플과_키_타입_구조가_같다(self):
        if not SAMPLE.exists():
            self.skipTest(f"합의 샘플 없음: {SAMPLE}")
        sample = json.loads(SAMPLE.read_text(encoding="utf-8-sig"))
        actual, expected = _type_shape(self.ok_response()), _type_shape(sample)
        # 샘플은 사진 대신 overlay.image를 null로 둔다 → 그때는 문자열 또는 null 둘 다 허용
        if sample["overlay"]["image"] is None:
            self.assertIn(actual["overlay"]["image"], ("str", "NoneType"))
            actual["overlay"]["image"] = expected["overlay"]["image"]
        self.assertEqual(actual, expected)

    def test_필드가_없으면_400(self):
        for files, data in [(None, {"productId": "X"}),
                            ({"image": ("a.jpg", jpeg_bytes(), "image/jpeg")}, None),
                            ({"image": ("a.jpg", jpeg_bytes(), "image/jpeg")}, {"productId": "  "}),
                            ({"image": ("a.jpg", b"not an image", "image/jpeg")}, {"productId": "X"})]:
            r = self.post(files=files, data=data)
            self.assertEqual(r.status_code, 400, r.text)
            self.assertEqual(r.json()["code"], "BAD_REQUEST")
            self.assertIn("message", r.json())

    def test_보드를_못_찾으면_422(self):
        with mock.patch.object(main, "crop_to_roi", side_effect=ValueError("열이 14개가 아니라 9개")):
            r = self.client.post("/recipe-draft", files={"image": ("a.jpg", jpeg_bytes(), "image/jpeg")},
                                 data={"productId": "X"})
        self.assertEqual(r.status_code, 422)
        self.assertEqual(r.json()["code"], "BOARD_NOT_RECOGNIZED")

    def test_그_밖의_오류는_500(self):
        with mock.patch.object(main, "crop_to_roi", return_value=(self.roi, 1.0, [])), \
             mock.patch.object(main, "all_holes_from_image", return_value=(self.roi, self.holes)), \
             mock.patch.object(main, "detect_parts", side_effect=RuntimeError("model broken")):
            r = self.client.post("/recipe-draft", files={"image": ("a.jpg", jpeg_bytes(), "image/jpeg")},
                                 data={"productId": "X"})
        self.assertEqual(r.status_code, 500)
        self.assertEqual(r.json()["code"], "INTERNAL_ERROR")


@unittest.skipUnless(WEIGHTS.exists() and TEST1.exists(), "가중치나 C:\\temp\\test1.jpg 없음")
class InferRegressionTests(unittest.TestCase):
    """test1.jpg의 /infer 응답이 변경 전 코드(fixtures/infer_test1_baseline.json)와 바이트까지 같은지."""

    def test_infer_응답이_변경_전과_같다(self):
        base = json.loads((FIXTURES / "infer_test1_baseline.json").read_text(encoding="utf-8"))
        img = TEST1.read_bytes()
        self.assertEqual(hashlib.sha256(img).hexdigest(), base["imageSha256"], "기준을 만들 때와 다른 test1.jpg")
        client = TestClient(main.app)
        plain = client.post("/infer", files={"image": ("test1.jpg", img, "image/jpeg")})
        overlay = client.post("/infer?overlay=true", files={"image": ("test1.jpg", img, "image/jpeg")})
        self.assertEqual(plain.json(), base["infer"])
        self.assertEqual(hashlib.sha256(plain.content).hexdigest(), base["inferSha256"])
        self.assertEqual(overlay.json()["parts"], base["inferOverlayParts"])
        self.assertEqual(hashlib.sha256(overlay.content).hexdigest(), base["inferOverlaySha256"])


if __name__ == "__main__":
    unittest.main()
