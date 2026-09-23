def judge(contours):
    """diff 영역(contours)이 하나라도 있으면 FAIL, 없으면 PASS.

    반환: (result: "PASS" | "FAIL", num_diff_regions: int)
    """
    num_regions = len(contours)
    result = "FAIL" if num_regions > 0 else "PASS"
    return result, num_regions
