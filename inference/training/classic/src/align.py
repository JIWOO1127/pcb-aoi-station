import cv2
import numpy as np


def align_by_features(golden_img, input_img, mask=None,
                       max_features=2000, good_match_ratio=0.75,
                       ransac_thresh=5.0, min_matches=10):
    """ORB 특징점 매칭 + RANSAC homography로 input_img를 golden_img 좌표계에 정렬.

    mask: 특징점 검출을 제한할 영역(golden_img와 같은 크기의 8bit 마스크).
          브레드보드처럼 반복 패턴이 많은 경우, 파워레일/텍스트/테두리 등
          고유한 영역만 남기면 오매칭이 줄어든다.

    반환: (aligned_img, H, num_inliers, valid_mask)
    valid_mask: aligned_img에서 실제 input_img 픽셀로 채워진 영역은 255,
                정렬 중 이미지 밖으로 밀려나 비어버린(검은) 영역은 0.
                이 마스크 밖은 diff 비교에서 제외해야 한다.
    """
    gray_golden = cv2.cvtColor(golden_img, cv2.COLOR_BGR2GRAY) if golden_img.ndim == 3 else golden_img
    gray_input = cv2.cvtColor(input_img, cv2.COLOR_BGR2GRAY) if input_img.ndim == 3 else input_img

    orb = cv2.ORB_create(max_features)
    kp_golden, des_golden = orb.detectAndCompute(gray_golden, mask)
    kp_input, des_input = orb.detectAndCompute(gray_input, mask)

    if des_golden is None or des_input is None:
        raise ValueError("특징점을 검출하지 못했습니다.")

    bf = cv2.BFMatcher(cv2.NORM_HAMMING)
    raw_matches = bf.knnMatch(des_input, des_golden, k=2)

    good_matches = [m for m, n in raw_matches if m.distance < good_match_ratio * n.distance]

    if len(good_matches) < min_matches:
        raise ValueError(f"정렬에 필요한 매칭점 부족: {len(good_matches)}개 (최소 {min_matches}개 필요)")

    src_pts = np.float32([kp_input[m.queryIdx].pt for m in good_matches]).reshape(-1, 1, 2)
    dst_pts = np.float32([kp_golden[m.trainIdx].pt for m in good_matches]).reshape(-1, 1, 2)

    H, inlier_mask = cv2.findHomography(src_pts, dst_pts, cv2.RANSAC, ransac_thresh)
    if H is None:
        raise ValueError("Homography 계산 실패")

    h, w = golden_img.shape[:2]
    aligned = cv2.warpPerspective(input_img, H, (w, h))

    input_full = np.full(input_img.shape[:2], 255, dtype=np.uint8)
    valid_mask = cv2.warpPerspective(input_full, H, (w, h))

    num_inliers = int(inlier_mask.sum()) if inlier_mask is not None else 0
    return aligned, H, num_inliers, valid_mask
