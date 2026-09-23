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

# 아래 픽셀 기반 상수들은 전부 기준 해상도(scale.py, 회전 후 3024×4032) 실측
# 기준이다. 실제 이미지 해상도가 다르면 find_pinhole_pixels에 넘긴 scale로
# 비례 조정한다 (거리는 scale 1제곱, 넓이는 scale 2제곱 — 각 사용처 참고).
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
# (10이었다가 13으로 올림: 라벨 잡음 그룹이 실측으로 최대 n=10까지 나온 적이
# 있어서, 실제 열의 최소 개수(실측 n=15 이상)와 확실히 구분되도록 여유를 둠)
# 이건 "개수"라서 해상도와 무관 — scale로 조정하지 않는다.
_MIN_COL_GROUP_SIZE = 13

# 같은 열 안에서, 이 픽셀 이내로 붙어있으면 같은 홀(중첩 컨투어)로 보고 하나로 합친다.
_ROW_DEDUP_GAP_PX = 8
# 한 칸(행) 피치로 인정할 값의 범위. 실측(~50~60px) 기준으로 넉넉히 잡되,
# 중복(잡음, <10px)이나 E/K/Q를 건너뛴 2~3칸짜리 큰 간격은 제외한다.
_PITCH_DIFF_MIN = 35
_PITCH_DIFF_MAX = 70
# 평면 피팅에서 이 픽셀보다 잔차가 크면 이상치(잘못 매칭된 점)로 보고 뺀 뒤 재피팅한다.
_PLANE_OUTLIER_PX = 15

# 한 열의 실제 검출 개수가 "이 열의 기대 개수 - 이 값"보다 적으면, 그 열의
# 1차 앵커(마지막 점=마지막 행)를 못 믿는 것으로 본다(개수라서 scale 무관).
_TRUSTED_COL_SLACK = 3

N_COLS = pinhole_grid.COL_MAX - pinhole_grid.COL_MIN + 1  # 14
N_ROWS = len(pinhole_grid.ROWS)  # 21

Point = tuple[float, float]


@dataclass
class DetectedBoard:
    """검출된 보드 이미지 + 핀홀 282개의 픽셀 위치."""

    image: np.ndarray  # 입력 image 그대로 (변형 없음)
    pixel_by_label: dict[str, Point]  # 핀홀 라벨("J9") -> image 기준 픽셀 좌표


def find_pinhole_pixels(
    image: np.ndarray, scale: float = 1.0, label_anchors: list[tuple[int, float, float]] | None = None
) -> DetectedBoard:
    """크롭된 보드 이미지 하나에서, 282개 핀홀의 실제 픽셀 위치를 찾는다.

    이미지 자체는 변형하지 않는다 (입력 그대로 반환) — 원근 변환을 썼다가
    렌즈 왜곡을 오히려 키우는 걸 확인해서 뺐다 (모듈 docstring 참고).

    scale: `scale.compute_scale`로 구한 해상도 배율 (기준 해상도면 1.0). 이
    모듈의 픽셀 기반 상수들은 전부 기준 해상도로 튜닝돼 있어서, 다른
    해상도면 이 배율로 비례 조정한다.

    label_anchors: `roi.crop_to_roi`가 반환한 (행번호, x, y) 라벨 앵커
    (크롭 후 좌표계). 있으면 `_fit_row_plane`이 열별 추정보다 이걸 우선한다.

    홀을 충분히 찾지 못하면 ValueError.
    """
    centroids = _find_hole_centroids(image, scale)
    col_xs = _fit_columns(centroids, scale)

    col_match_radius = _COL_MATCH_RADIUS * scale
    pitch_diff_min = _PITCH_DIFF_MIN * scale
    pitch_diff_max = _PITCH_DIFF_MAX * scale

    # 열마다 y좌표를 모은다(중복 제거). `_fill_missing_columns`로 산수 보간된
    # 열은 애초에 실제 검출된 홀이 없거나(또는 너무 적어서) 근처에서 y를
    # 못 모을 수 있다 — 그런 열은 평면을 피팅하는 데는 안 쓰고 건너뛴다.
    # 최종 위치는 어차피 (다른 열들로 피팅된) 평면식으로 계산하므로 문제없다.
    #
    # 열마다 "이 열의 앵커(마지막 점=U행)를 믿을 만한가"도 같이 표시해서
    # `_fit_row_plane`에 넘긴다 — 실측 개수가 이 열의 기대 개수(레일 18,
    # 단자부 21)에 너무 못 미치면 못 믿는다. 레일 열은 단자부보다 짧게
    # 끝나는 보드가 있었고(실측: new_test_images/board4.jpg), 단자부 열도
    # IC 등에 가려 절반만 잡히는 경우가 있어서(실측: board2.jpg) — 둘 다
    # "이 열의 마지막 점이 진짜 마지막 행이다"라는 가정이 깨진다.
    col_ys: list[list[float]] = []
    plane_fit_xs: list[float] = []
    trusted: list[bool] = []
    pitch_samples: list[float] = []
    for col_index, col_x in enumerate(col_xs):
        expected_count = len(_expected_rows_for_col(col_index))
        ys_here = _dedup(sorted(y for x, y in centroids if abs(x - col_x) < col_match_radius), scale)
        if len(ys_here) < 2:
            continue
        plane_fit_xs.append(col_x)
        col_ys.append(ys_here)
        # 레일 열(기대 개수 < N_ROWS)은 개수가 다 차도 앵커로 안 믿는다 —
        # 실측해보니 레일이 단자부보다 짧게 끝나는 보드가 있어서, "이 열의
        # 마지막 점 = U행"이라는 가정 자체가 레일에서는 개수와 무관하게
        # 깨질 수 있었다 (실측: new_test_images/board4.jpg — WORKLOG.md 참고).
        is_terminal_col = expected_count == N_ROWS
        trusted.append(is_terminal_col and len(ys_here) >= expected_count - _TRUSTED_COL_SLACK)
        for a, b in zip(ys_here, ys_here[1:]):
            diff = b - a
            if pitch_diff_min <= diff <= pitch_diff_max:
                pitch_samples.append(diff)

    if len(plane_fit_xs) < 2:
        raise ValueError(f"평면을 피팅할 열이 부족합니다 ({len(plane_fit_xs)}개).")
    if not pitch_samples:
        raise ValueError("칸 간격(피치)을 추정할 표본을 찾지 못했습니다.")
    pitch = float(np.median(pitch_samples))

    if sum(trusted) < 2:
        trusted = [True] * len(trusted)  # 믿을 열이 너무 적으면 전부 믿는 것으로 대체(안전망)

    coef = _fit_row_plane(plane_fit_xs, col_ys, pitch, scale, trusted, label_anchors)

    pixel_by_label: dict[str, Point] = {}
    for col_index, col_x in enumerate(col_xs):
        for row_letter in _expected_rows_for_col(col_index):
            true_row_index = pinhole_grid.ROWS.index(row_letter)
            y = coef[0] + coef[1] * true_row_index + coef[2] * col_x
            pixel_by_label[f"{row_letter}{col_index}"] = (col_x, float(y))

    return DetectedBoard(image=image, pixel_by_label=pixel_by_label)


def _fit_row_plane(
    col_xs: list[float],
    col_ys: list[list[float]],
    pitch: float,
    scale: float = 1.0,
    trusted: list[bool] | None = None,
    label_anchors: list[tuple[int, float, float]] | None = None,
) -> np.ndarray:
    """모든 열의 홀을 한꺼번에 써서 `y = b0 + b1*행번호 + b2*x` 평면을 피팅한다.

    이 평면 하나로 모든 열의 모든 행 위치를 계산하므로, 같은 행번호는
    정의상 항상 일직선이 된다 (평면이 x에 대해 선형이라서) — 열마다 따로
    선형보간했을 때 생기던 "같은 행인데 열마다 살짝 다른 높이" 문제가
    구조적으로 없어진다 (모듈 docstring 참고).

    **1차 앵커(가장 중요한 부분)**: 우선순위는 다음과 같다.

    1. `label_anchors`(OCR로 찾은 "10"~"30" 라벨의 실제 (행번호, x, y))가
       충분하면(3개 이상, x좌표도 어느 정도 퍼져 있으면) 이걸로 1차
       평면을 바로 피팅한다 — 열의 점 개수나 끝점 위치를 전혀 안 쓰고,
       "이 라벨이 실제로 이 행이다"라는 직접 관측값만 쓰므로 가장 믿을
       만하다. 다만 라벨 인식 자체가 실측 위치보다 살짝 어긋나 있을 수도
       있어서, 재배정 결과가 말이 안 되면(유효 범위에 드는 점이 너무
       적으면) 이 결과를 버리고 2번으로 넘어간다.
    2. `trusted`(열별로 "이 열의 앵커[마지막 점=U행]를 믿어도 되는지")로
       걸러진 열들만으로 "이 열의 반대쪽 끝(신뢰할 수 있는 쪽)과 전역
       피치로 역산"하는 기존 방식. 열의 반대쪽 끝 자체가 부품에 가려
       일찍 끊기거나(예: IC나 전선이 열의 마지막 몇 홀을 가림), 레일
       열처럼 원래 단자부와 물리적 길이가 다른 경우(실측:
       new_test_images/board4.jpg) 이 가정이 깨질 수 있어서, 그런 열은
       `trusted=False`로 걸러 1차 피팅에서 아예 뺀다.

    어느 쪽으로 1차 피팅을 하든, 그 평면으로 **모든 열의** 행 번호를
    다시 매긴 뒤 재피팅한다 — 1차가 이미 대체로 맞는 평면을 주므로, 그
    평면 기준으로 다시 매기면 1차에서 제외했던 열도 바로잡힌다.
    (실측: board1.jpg, board2.jpg, new_test_images/board4·5.jpg —
    WORKLOG.md의 "라벨 기반 행 앵커" 참고.)
    """
    if trusted is None:
        trusted = [True] * len(col_xs)

    def _assign_by_local_anchor() -> list[tuple[float, int, float]]:
        rows = []
        for col_x, ys_here, is_trusted in zip(col_xs, col_ys, trusted):
            if not is_trusted:
                continue
            predicted_top = ys_here[-1] - (N_ROWS - 1) * pitch
            for y in ys_here:
                row_index = round((y - predicted_top) / pitch)
                if 0 <= row_index <= N_ROWS - 1:
                    rows.append((col_x, row_index, y))
        return rows

    def _assign_by_plane(coef: np.ndarray) -> list[tuple[float, int, float]]:
        rows = []
        for col_x, ys_here in zip(col_xs, col_ys):
            for y in ys_here:
                row_index = round((y - coef[0] - coef[2] * col_x) / coef[1])
                if 0 <= row_index <= N_ROWS - 1:
                    rows.append((col_x, row_index, y))
        return rows

    def _lstsq(rows: list[tuple[float, int, float]]) -> np.ndarray:
        data = np.array(rows, dtype=np.float64)
        design = np.column_stack([np.ones(len(data)), data[:, 1], data[:, 0]])
        target = data[:, 2]
        coef, *_ = np.linalg.lstsq(design, target, rcond=None)
        return coef

    def _lstsq_with_outlier_rejection(rows: list[tuple[float, int, float]]) -> np.ndarray:
        if len(rows) < 10:
            raise ValueError(f"행 평면을 피팅할 표본이 부족합니다 ({len(rows)}개).")
        coef = _lstsq(rows)
        data = np.array(rows, dtype=np.float64)
        design = np.column_stack([np.ones(len(data)), data[:, 1], data[:, 0]])
        target = data[:, 2]
        residuals = target - design @ coef
        keep = np.abs(residuals) < _PLANE_OUTLIER_PX * scale
        if keep.sum() >= 10:
            coef, *_ = np.linalg.lstsq(design[keep], target[keep], rcond=None)
        return coef

    def _try_label_anchor_plane() -> np.ndarray | None:
        if not label_anchors or len(label_anchors) < 3:
            return None
        xs = [x for _, x, _ in label_anchors]
        if max(xs) - min(xs) < pitch * 3:
            return None  # x가 한쪽에 몰려있으면 기울기(coef2)를 못 믿음
        coef = _lstsq([(x, row_index, y) for row_index, x, y in label_anchors])
        # 이 평면으로 실제 검출 데이터를 재배정했을 때, 열마다 최소 몇 개는
        # 유효 행 범위(0~N_ROWS-1) 안에 들어와야 "말이 되는 평면"으로 본다.
        if len(_assign_by_plane(coef)) < len(col_xs) * 3:
            return None
        return coef

    coef = _try_label_anchor_plane()
    if coef is None:
        coef = _lstsq_with_outlier_rejection(_assign_by_local_anchor())
    # 1차 평면 기준으로 행 번호를 다시 매기고 재피팅 (부품에 가려 한쪽 끝이
    # 틀린 열도, 라벨 앵커가 없던 열도 전역 평면 기준으로 바로잡힘).
    coef = _lstsq_with_outlier_rejection(_assign_by_plane(coef))
    return coef


def _dedup(sorted_ys: list[float], scale: float = 1.0) -> list[float]:
    """가까이 붙은 값(같은 홀의 중첩 컨투어)을 하나로 합친다."""
    if not sorted_ys:
        return []
    gap_px = _ROW_DEDUP_GAP_PX * scale
    result = [sorted_ys[0]]
    for y in sorted_ys[1:]:
        if y - result[-1] > gap_px:
            result.append(y)
    return result


def _expected_rows_for_col(col_index: int) -> list[str]:
    """이 열 번호에 실제로 존재하는 행 글자 목록 (E/K/Q는 레일 열에서 빠짐)."""
    return [r for r in pinhole_grid.ROWS if pinhole_grid.col_range(r)[0] <= col_index <= pinhole_grid.col_range(r)[1]]


def _find_hole_centroids(image: np.ndarray, scale: float = 1.0) -> np.ndarray:
    """이미지에서 어두운 사각형(홀) blob들의 중심 좌표를 찾는다."""
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    _, thresh = cv2.threshold(gray, _DARK_THRESHOLD, 255, cv2.THRESH_BINARY_INV)
    contours, _ = cv2.findContours(thresh, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)

    # 넓이는 선형 배율의 제곱만큼 변한다 (가로·세로 둘 다 줄어드니까).
    area_scale = scale * scale
    min_blob_area = _MIN_BLOB_AREA * area_scale
    max_blob_area = _MAX_BLOB_AREA * area_scale

    centroids = []
    for c in contours:
        area = cv2.contourArea(c)
        if area < min_blob_area or area > max_blob_area:
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


def _fit_columns(centroids: np.ndarray, scale: float = 1.0) -> list[float]:
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
    gap_px = _COL_GROUP_GAP_PX * scale

    groups: list[list[float]] = [[xs[0]]]
    for x in xs[1:]:
        if x - groups[-1][-1] <= gap_px:
            groups[-1].append(x)
        else:
            groups.append([x])

    # 부품(IC 다리, 저항 리드 등)이 홀을 가리면, 한 열의 점들이 한가운데서
    # 살짝 끊겨 여러 조각으로 쪼개질 수 있다 (실측: new_test_images/board2.jpg,
    # board3.jpg — WORKLOG.md 참고). 확실한(개수가 충분한) 그룹들 사이 간격으로
    # 진짜 열 피치를 추정한 뒤, 그 피치의 절반보다 가까이 붙은 조각들은 같은
    # 열로 보고 다시 합친다.
    big_groups = [g for g in groups if len(g) >= _MIN_COL_GROUP_SIZE]
    if len(big_groups) >= 2:
        centers = sorted(float(np.mean(g)) for g in big_groups)
        approx_pitch = float(np.median([b - a for a, b in zip(centers, centers[1:])]))
        merge_gap = approx_pitch / 2

        merged: list[list[float]] = [groups[0]]
        for g in groups[1:]:
            prev = merged[-1]
            # 둘 다 이미 "확실한 열"이면 합치지 않는다 — 실측 피치가 열마다
            # 살짝 달라서(단자부 채널 등) 우연히 간격이 좁은 두 진짜 열을
            # 하나로 잘못 합치는 걸 막기 위함. 둘 중 하나가 조각(잡음)일 때만 합친다.
            either_is_fragment = len(prev) < _MIN_COL_GROUP_SIZE or len(g) < _MIN_COL_GROUP_SIZE
            # 합친 결과가 실제 행 개수(N_ROWS)보다 훨씬 커지면, 조각인 줄 알았던
            # 것이 사실 그다음 진짜 열의 시작이라는 뜻이다 — 이 경우 합치지 않는다.
            not_too_big = len(prev) + len(g) <= N_ROWS + 3
            if either_is_fragment and not_too_big and g[0] - prev[-1] <= merge_gap:
                merged[-1] = prev + g
            else:
                merged.append(g)
        groups = merged

    real_columns = [g for g in groups if len(g) >= _MIN_COL_GROUP_SIZE]

    if len(real_columns) > N_COLS:
        # 레일과 첫 단자부 사이처럼 원래 비어있어야 할 자리에 노이즈(실크
        # 스크린 반사 등)가 우연히 많이 뭉치면, 개수만으로는 진짜 열과
        # 구분이 안 될 수 있다 (실측: new_test_images/board5.jpg — 노이즈가
        # 13개나 모여서 진짜 열처럼 잡힘. WORKLOG.md 참고). 그런데 이런
        # 노이즈 뭉치는 진짜 열(x폭 1~6px)과 달리 x폭이 훨씬 넓다(20px+).
        # 넘치는 개수만큼, x폭이 가장 넓은 그룹부터 의심스러운 것으로 보고
        # 제외한다.
        real_columns = sorted(real_columns, key=lambda g: max(g) - min(g))[:N_COLS]

    col_xs = sorted(float(np.mean(g)) for g in real_columns)

    if len(col_xs) < N_COLS:
        # 부품에 가려서 "확실한 열"로 인정받을 만큼(≥_MIN_COL_GROUP_SIZE)
        # 점이 안 모인 열이 있을 수 있다. 어디가 빈 자리인지 먼저 산수로
        # 찾은 뒤, **그 자리에 마침 실제로 검출된 조각(개수는 부족해도
        # 위치는 진짜 측정값)이 있으면 그 실측 위치를 우선 쓰고**, 없으면
        # 산수로 계산한 위치를 그대로 쓴다 — 렌즈 왜곡 등으로 피치가 구간마다
        # 살짝 달라서, 산수 위치가 실제보다 몇 픽셀 어긋날 수 있기 때문
        # (실측: new_test_images/board2.jpg의 IC칩 옆 열 — WORKLOG.md 참고).
        leftover = [g for g in groups if len(g) < _MIN_COL_GROUP_SIZE]
        col_xs = _fill_missing_columns(col_xs, leftover)

    if len(col_xs) != N_COLS:
        raise ValueError(
            f"열이 {N_COLS}개가 아니라 {len(real_columns)}개 검출됐습니다 "
            f"(그룹별 개수: {[len(g) for g in groups]})."
        )
    return col_xs


def _fill_missing_columns(col_xs: list[float], leftover_groups: list[list[float]] | None = None) -> list[float]:
    """확실하게 검출된 열들의 간격(피치)으로, 통째로 안 잡힌 열의 위치를 채운다.

    leftover_groups: 확실한 열로 인정 못 받은 작은 조각들(있으면). 산수로
    "여기가 빈 자리다"라고 찾은 위치 근처에 실제로 조각이 있으면, 순수
    산수 위치 대신 그 조각의 실측 평균 위치를 쓴다 — 그 반대(아무 자리에나
    큰 조각부터 끼워 맞추는 것)는 원래 정상적으로 넓어야 하는 구간(레일
    전환부 등)에도 조각을 잘못 끼워넣을 수 있어서, 반드시 "산수로 먼저
    빈 자리를 찾고 나서" 그 자리에 맞는 조각을 찾는 순서로 한다.

    열 배치는 [레일2열][단자부5열][채널][단자부5열][레일2열] 순서라 간격이
    균등하지 않다 — 레일 안쪽·단자부 안쪽 간격("정상 피치")은 서로 거의
    같지만, 레일↔단자부 전환부·채널("넓은 간격")은 그보다 훨씬 넓다(보통
    정상 피치의 3배 안팎). 열 하나가 통째로 안 잡히면 그 자리의 간격이:

    - 정상 구간 한가운데면 **정상 피치의 약 2배**로 보이고,
    - 넓은 전환부·채널 바로 옆이면 **"정상 피치 + 넓은 간격"만큼** 넓어져,
      다른 멀쩡한 넓은 간격들보다 눈에 띄게 더 넓게 나온다.

    두 번째 경우를 감지할 때는 "이 넓은 간격이 다른 넓은 간격들과 비교해도
    유난히 넓은가"를 봐야 한다 — 그냥 "정상 피치+넓은 간격과 비슷한가"만
    보면, 애초에 멀쩡한 넓은 간격도 우연히 비슷한 크기로 걸려서 오탐하고,
    심하면 무한정 열을 끼워넣는 버그로 이어진다(실측:
    new_test_images/board3.jpg에서 14개가 아니라 20개까지 채워버림 —
    WORKLOG.md 참고). 그래서 각 넓은 간격을, **자기 자신을 뺀 나머지 넓은
    간격들의 중앙값**과 비교해서(leave-one-out) 유난히 넓은 것만 "빠진 열이
    옆에 있다"고 판단한다.

    몇 개가 비었는지는 정확히 안다(N_COLS - 지금 개수) — 그만큼만 끼워넣고
    멈춘다. 이렇게 정해두지 않으면, 하나 끼워넣은 뒤 생긴 애매한 나머지
    간격이 우연히 또 "정상 피치의 2배"에 가까워 보여서 계속 잘못 끼워넣는
    경우가 있었다 (실측: new_test_images/board2.jpg — WORKLOG.md 참고).
    """
    _RELATIVE_TOLERANCE = 0.3
    _WIDE_CONSISTENCY_TOLERANCE = 0.1  # 넓은 간격들끼리는 이 오차 이내로 서로 비슷해야 "정상"

    leftover = list(leftover_groups) if leftover_groups else []
    result = list(col_xs)
    while len(result) < N_COLS:
        gaps = [b - a for a, b in zip(result, result[1:])]
        if not gaps:
            break

        rough_pitch = float(np.median(gaps))
        normal_gaps = [g for g in gaps if g < rough_pitch * 1.4]
        if not normal_gaps:
            break
        normal_pitch = float(np.median(normal_gaps))
        wide_gaps = [g for g in gaps if g >= rough_pitch * 1.4 and abs(g - 2 * normal_pitch) / (2 * normal_pitch) > _RELATIVE_TOLERANCE]

        best = None  # (오차, 삽입 위치 인덱스, x좌표)

        def _consider(i: int, pos: float, error: float) -> None:
            nonlocal best
            if error > _RELATIVE_TOLERANCE:  # 허용 오차 밖이면 아예 후보로 안 봄
                return
            margin = normal_pitch * 0.3
            if not (result[i] + margin < pos < result[i + 1] - margin):
                return
            if best is None or error < best[0]:
                best = (error, i, pos)

        for i, gap in enumerate(gaps):
            # 1) 정상 구간 한가운데 하나 빔 (정상 피치의 2배).
            _consider(i, (result[i] + result[i + 1]) / 2, abs(gap - 2 * normal_pitch) / (2 * normal_pitch))

            # 2) 넓은 전환부 바로 옆에 하나 빔 — 이 간격이 "다른 넓은 간격들"보다
            #    유난히 넓을 때만 (leave-one-out 비교).
            if gap in wide_gaps and len(wide_gaps) >= 2:
                other_wide = list(wide_gaps)
                other_wide.remove(gap)
                if other_wide:
                    peer_pitch = float(np.median(other_wide))
                    consistency_error = abs(gap - peer_pitch) / peer_pitch
                    if consistency_error > _WIDE_CONSISTENCY_TOLERANCE:
                        expected = normal_pitch + peer_pitch
                        error = abs(gap - expected) / expected
                        if error <= _RELATIVE_TOLERANCE:
                            _consider(i, result[i] + normal_pitch, error)
                            _consider(i, result[i + 1] - normal_pitch, error)

        if best is None:
            break
        _, i, pos = best

        # 산수로 "여기가 빈 자리다"라고 찾은 이 gap 안에 실제로 검출된
        # 조각이 있으면, 산수 위치가 아니라 그 실측 위치를 우선 쓴다 — 렌즈
        # 왜곡 등으로 산수 위치 자체가 실제보다 십수 픽셀 어긋날 수 있어서,
        # 산수 위치와 가까운지는 안 보고 "이 gap 안(양 끝에서 충분히 떨어진
        # 곳)에 있는지"만 본다 (실측: new_test_images/board2.jpg —
        # WORKLOG.md 참고). 여러 개면 점이 더 많은(신뢰도 높은) 것을 쓴다.
        margin = normal_pitch * 0.3
        candidates = [g for g in leftover if result[i] + margin < float(np.mean(g)) < result[i + 1] - margin]
        if candidates:
            best_fragment = max(candidates, key=len)
            leftover.remove(best_fragment)
            pos = float(np.mean(best_fragment))

        result.insert(i + 1, pos)

    return result
