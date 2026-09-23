import os
import sys

_PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))

sys.path.insert(0, os.path.join(_PROJECT_ROOT, "src"))
sys.path.insert(0, os.path.join(_PROJECT_ROOT, "config"))

import cv2
import numpy as np

from crop import crop_fixed
from align import align_by_features
from diff import compute_diff
from segment import segment_diff
from judge import judge
from settings import CROP_BOX, DIFF_BLUR_KSIZE, MORPH_KSIZE, MIN_AREA


def imread_unicode(path):
    return cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_COLOR)


def run(golden_path, test_path):
    """선택 UI가 'classic' 모델을 실행할 때 호출하는 표준 진입점."""
    golden_raw = imread_unicode(golden_path)
    test_raw = imread_unicode(test_path)

    x, y, w, h = CROP_BOX
    golden = crop_fixed(golden_raw, x, y, w, h)
    test = crop_fixed(test_raw, x, y, w, h)

    aligned, H, inliers, valid_mask = align_by_features(golden, test)
    valid_mask = cv2.erode(valid_mask, np.ones((15, 15), np.uint8))

    diff_img = compute_diff(golden, aligned, blur_ksize=DIFF_BLUR_KSIZE)

    valid_pixels = diff_img[valid_mask > 0]
    otsu_thresh, _ = cv2.threshold(
        valid_pixels.reshape(-1, 1), 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU
    )
    diff_img = cv2.bitwise_and(diff_img, diff_img, mask=valid_mask)

    binary, contours = segment_diff(
        diff_img, thresh_val=otsu_thresh, morph_ksize=MORPH_KSIZE, min_area=MIN_AREA
    )
    result, num_regions = judge(contours)

    return {
        "result": result,
        "num_diff_regions": num_regions,
        "align_inliers": inliers,
    }


if __name__ == "__main__":
    default_golden = os.path.join(_PROJECT_ROOT, "data", "golden", "golden.jpg")
    default_test = os.path.join(_PROJECT_ROOT, "data", "test_set", "ng", "ng_1.jpg")

    golden_path = sys.argv[1] if len(sys.argv) > 1 else default_golden
    test_path = sys.argv[2] if len(sys.argv) > 2 else default_test

    info = run(golden_path, test_path)
    print(f"판정: {info['result']}")
    print(f"diff 영역 수: {info['num_diff_regions']}")
    print(f"정렬 inlier 수: {info['align_inliers']}")
