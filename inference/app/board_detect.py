"""크롭된 보드 이미지에서 핀홀 282개의 실제 픽셀 위치를 직접 검출한다.

**시도했다가 버린 방법 (기록):** 네 모서리(A0/A13/U0/U13)로 원근 변환
(perspective warp)해서 이미지를 평면처럼 편 다음 그리드를 검출하는 방식을
써봤는데, `board1.jpg`(부품 많은 사진)에서 실측해보니 오히려 왜곡이 더
심해졌다. 원본 크롭에서 행 A(맨 윗줄)의 실제 y좌표가 열마다 36~52px 사이로
완만하게 처져 있었는데(카메라 렌즈 왜곡으로 보임 — 호모그래피로 못 고치는
종류), 모서리 4개짜리 호모그래피는 이걸 "두 끝점을 잇는 직선"으로 가정하고
나머지를 선형보간하기 때문에, 평탄화 후에는 7~50px까지 더 크게 출렁였다.
게다가 행 y좌표를 모든 열을 합쳐서 한 번에 전역 k-means로 구했더니 이
왜곡이 더 섞였다. (WORKLOG.md 참고)

**두 번째로 시도했다가 버린 방법:** 열마다 독립적으로 그 열 자신의 첫
홀~마지막 홀 사이를 선형보간하는 방식(이미지 변형은 없음). 그런데 사용자가
지적한 대로 **같은 알파벳(행)의 핀홀은 항상 일직선이어야 하는데**, 열마다
따로 계산하면 이 제약이 보장되지 않는다 — 열마다 독립적인 선형보간이 조금씩
어긋나면서, 여러 열을 가로질러 보면 같은 행이 일직선이 아니라 살짝 휘어
보이는 문제가 있었다 (`board1.jpg`에서 실측 확인됨. WORKLOG.md 참고).

그래서 지금은:
1. 이미지(크롭된 그대로, 안 편 상태)에서 홀(구멍) blob을 전부 찾는다.
2. x좌표를 정렬해서 간격이 가까운 것끼리 묶어(gap clustering) 실제 열
   14개의 위치를 구한다 (열 6~7 사이 물리적 갭 때문에 균등하지 않은 간격을
   그대로 보존 — `_fit_columns` 참고, k-means 대신 이 방식을 쓰는 이유도
   거기 적혀 있다).
3. 홀 하나하나에 "이 홀이 몇 번째 행인지" 추정치를 붙인 다음, **모든 열의
   모든 홀을 한꺼번에** 이용해서 `y = b0 + b1*행번호 + b2*x` 평면 하나를
   최소제곱으로 피팅한다(`_fit_row_plane`). 이 평면 하나로 모든 열의 모든
   행 위치를 계산하므로, **같은 행번호는 정의상 항상 일직선**이 된다
   (평면이 x에 대해 선형이라서). 열마다 따로 계산하지 않기 때문에 열 사이
   기울기가 조금씩 달라서 생기던 비일관성이 사라진다.

`find_pinhole_pixels(image)` 하나만 호출하면 된다.
"""

from dataclasses import dataclass

import cv2
import numpy as np

import pinhole_grid

_MIN_BLOB_AREA = 20
_MAX_BLOB_AREA = 400
_MIN_ASPECT = 0.5
_MAX_ASPECT = 2.0
_DARK_THRESHOLD = 100

# 같은 열로 묶을 x축 허용 오차. 실측 최소 열 간격(~55px)의 절반보다 작게 잡는다.
_COL_MATCH_RADIUS = 15

# 열 x좌표를 묶을 때, 이 픽셀 이내면 같은 열로 본다 (실측 열 내부 편차 ~13px 이내).
_COL_GROUP_GAP_PX = 10
# 이 개수 미만으로 뭉친 그룹은 실제 열이 아니라 잡음(행 번호 라벨 등)으로 간주해 버린다.
_MIN_COL_GROUP_SIZE = 10

# 같은 열 안에서, 이 픽셀 이내로 붙어있으면 같은 홀(중첩 컨투어)로 보고 하나로 합친다.
_ROW_DEDUP_GAP_PX = 8
# 한 칸(행) 피치로 인정할 값의 범위. 실측(~50~60px) 기준으로 넉넉히 잡되,
# 중복(잡음, <10px)이나 E/K/Q를 건너뛴 2~3칸짜리 큰 간격은 제외한다.
_PITCH_DIFF_MIN = 35
_PITCH_DIFF_MAX = 70
# 평면 피팅에서 이 픽셀보다 잔차가 크면 이상치(잘못 매칭된 점)로 보고 뺀 뒤 재피팅한다.
_PLANE_OUTLIER_PX = 15

N_COLS = pinhole_grid.COL_MAX - pinhole_grid.COL_MIN + 1  # 14
N_ROWS = len(pinhole_grid.ROWS)  # 21

Point = tuple[float, float]


@dataclass
class DetectedBoard:
    """검출된 보드 이미지 + 핀홀 282개의 픽셀 위치."""

    image: np.ndarray  # 입력 image 그대로 (변형 없음)
    pixel_by_label: dict[str, Point]  # 핀홀 라벨("J9") -> image 기준 픽셀 좌표


def find_pinhole_pixels(image: np.ndarray) -> DetectedBoard:
    """크롭된 보드 이미지 하나에서, 282개 핀홀의 실제 픽셀 위치를 찾는다.

    이미지 자체는 변형하지 않는다 (입력 그대로 반환) — 원근 변환을 썼다가
    렌즈 왜곡을 오히려 키우는 걸 확인해서 뺐다 (모듈 docstring 참고).

    홀을 충분히 찾지 못하면 ValueError.
    """
    centroids = _find_hole_centroids(image)
    col_xs = _fit_columns(centroids)

    # 열마다 y좌표를 모은다(중복 제거).
    col_ys: list[list[float]] = []
    pitch_samples: list[float] = []
    for col_x in col_xs:
        ys_here = _dedup(sorted(y for x, y in centroids if abs(x - col_x) < _COL_MATCH_RADIUS))
        if len(ys_here) < 2:
            raise ValueError(f"x={col_x:.0f} 열에서 홀을 충분히 찾지 못했습니다 ({len(ys_here)}개).")
        col_ys.append(ys_here)
        for a, b in zip(ys_here, ys_here[1:]):
            diff = b - a
            if _PITCH_DIFF_MIN <= diff <= _PITCH_DIFF_MAX:
                pitch_samples.append(diff)

    if not pitch_samples:
        raise ValueError("칸 간격(피치)을 추정할 표본을 찾지 못했습니다.")
    pitch = float(np.median(pitch_samples))

    coef = _fit_row_plane(col_xs, col_ys, pitch)

    pixel_by_label: dict[str, Point] = {}
    for col_index, col_x in enumerate(col_xs):
        for row_letter in _expected_rows_for_col(col_index):
            true_row_index = pinhole_grid.ROWS.index(row_letter)
            y = coef[0] + coef[1] * true_row_index + coef[2] * col_x
            pixel_by_label[f"{row_letter}{col_index}"] = (col_x, float(y))

    return DetectedBoard(image=image, pixel_by_label=pixel_by_label)


def _fit_row_plane(col_xs: list[float], col_ys: list[list[float]], pitch: float) -> np.ndarray:
    """모든 열의 홀을 한꺼번에 써서 `y = b0 + b1*행번호 + b2*x` 평면을 피팅한다.

    이 평면 하나로 모든 열의 모든 행 위치를 계산하므로, 같은 행번호는
    정의상 항상 일직선이 된다 (평면이 x에 대해 선형이라서) — 열마다 따로
    선형보간했을 때 생기던 "같은 행인데 열마다 살짝 다른 높이" 문제가
    구조적으로 없어진다 (모듈 docstring 참고).

    각 홀이 "몇 번째 행"인지는, 그 열의 반대쪽 끝(신뢰할 수 있는 쪽)과
    전역 피치로 역산해서 추정한다 (카메라가 살짝 기울어져 있으면 크롭
    경계에 A~U 범위 밖의 여분 홀이 하나 더 찍힐 수 있어서, 각 열의
    topmost/bottommost를 그대로 믿지 않는다 — WORKLOG.md 참고).
    """
    rows = []  # (x, row_index, y)
    for col_x, ys_here in zip(col_xs, col_ys):
        predicted_top = ys_here[-1] - (N_ROWS - 1) * pitch
        for y in ys_here:
            row_index = round((y - predicted_top) / pitch)
            if 0 <= row_index <= N_ROWS - 1:
                rows.append((col_x, row_index, y))

    if len(rows) < 10:
        raise ValueError(f"행 평면을 피팅할 표본이 부족합니다 ({len(rows)}개).")

    data = np.array(rows, dtype=np.float64)
    design = np.column_stack([np.ones(len(data)), data[:, 1], data[:, 0]])
    target = data[:, 2]

    coef, *_ = np.linalg.lstsq(design, target, rcond=None)
    # 이상치(occlusion·잡음으로 잘못 매칭된 점) 한 번 걸러내고 재피팅.
    residuals = target - design @ coef
    keep = np.abs(residuals) < _PLANE_OUTLIER_PX
    if keep.sum() >= 10:
        coef, *_ = np.linalg.lstsq(design[keep], target[keep], rcond=None)

    return coef


def _dedup(sorted_ys: list[float]) -> list[float]:
    """가까이 붙은 값(같은 홀의 중첩 컨투어)을 하나로 합친다."""
    result = [sorted_ys[0]]
    for y in sorted_ys[1:]:
        if y - result[-1] > _ROW_DEDUP_GAP_PX:
            result.append(y)
    return result


def _expected_rows_for_col(col_index: int) -> list[str]:
    """이 열 번호에 실제로 존재하는 행 글자 목록 (E/K/Q는 레일 열에서 빠짐)."""
    return [r for r in pinhole_grid.ROWS if pinhole_grid.col_range(r)[0] <= col_index <= pinhole_grid.col_range(r)[1]]


def _find_hole_centroids(image: np.ndarray) -> np.ndarray:
    """이미지에서 어두운 사각형(홀) blob들의 중심 좌표를 찾는다."""
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    _, thresh = cv2.threshold(gray, _DARK_THRESHOLD, 255, cv2.THRESH_BINARY_INV)
    contours, _ = cv2.findContours(thresh, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)

    centroids = []
    for c in contours:
        area = cv2.contourArea(c)
        if area < _MIN_BLOB_AREA or area > _MAX_BLOB_AREA:
            continue
        x, y, w, h = cv2.boundingRect(c)
        aspect = w / h if h else 0
        if not (_MIN_ASPECT <= aspect <= _MAX_ASPECT):
            continue
        m = cv2.moments(c)
        if m["m00"] == 0:
            continue
        centroids.append((m["m10"] / m["m00"], m["m01"] / m["m00"]))

    if len(centroids) < 4:
        raise ValueError(f"홀 blob을 충분히 찾지 못했습니다 ({len(centroids)}개 검출).")
    return np.array(centroids, dtype=np.float32)


def _fit_columns(centroids: np.ndarray) -> list[float]:
    """검출된 홀 중심들의 x좌표를 묶어, 실제 열 14개의 x좌표를 구한다.

    열 6~7 사이 물리적 갭(단자부 채널) 때문에 간격이 균등하지 않으므로,
    실제 검출값을 그대로 쓴다 (균등 가정 안 함).

    (처음엔 k=14로 고정한 k-means를 썼는데, 보드 실크스크린에 인쇄된 행
    번호 숫자("10","15",...)가 홀처럼 검출돼서 노이즈로 섞여 들어가면 특정
    열의 중심이 실제 위치에서 최대 13px까지 밀리는 걸 확인함. 대신 x좌표를
    정렬해서 **간격이 가까운 것끼리 묶는 방식**(rail_detect.py의
    _cluster_columns와 같은 원리)을 쓴다 — 숫자 라벨은 실제 열보다 점 개수가
    훨씬 적어서(전체 21행 중 1~2곳에서만 나타남) 개수로 걸러낼 수 있다.)
    """
    xs = sorted(float(x) for x, _ in centroids)

    groups: list[list[float]] = [[xs[0]]]
    for x in xs[1:]:
        if x - groups[-1][-1] <= _COL_GROUP_GAP_PX:
            groups[-1].append(x)
        else:
            groups.append([x])

    real_columns = [g for g in groups if len(g) >= _MIN_COL_GROUP_SIZE]
    if len(real_columns) != N_COLS:
        raise ValueError(
            f"열이 {N_COLS}개가 아니라 {len(real_columns)}개 검출됐습니다 "
            f"(그룹별 개수: {[len(g) for g in groups]})."
        )
    return sorted(float(np.mean(g)) for g in real_columns)
