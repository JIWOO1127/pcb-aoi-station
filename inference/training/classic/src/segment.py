import cv2


def segment_diff(diff_img, thresh_val=None, morph_ksize=5, min_area=50):
    """diff 이미지를 이진화 -> 모폴로지 정리 -> contour 추출.

    thresh_val: None이면 Otsu 자동 임계값, 값을 주면 고정 임계값 사용.
    min_area: 이 값보다 작은 contour는 노이즈로 간주해 제외.

    반환: (binary_mask, contours)
    """
    if thresh_val is None:
        _, binary = cv2.threshold(diff_img, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    else:
        _, binary = cv2.threshold(diff_img, thresh_val, 255, cv2.THRESH_BINARY)

    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (morph_ksize, morph_ksize))
    binary = cv2.morphologyEx(binary, cv2.MORPH_OPEN, kernel)
    binary = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, kernel)

    contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    contours = [c for c in contours if cv2.contourArea(c) >= min_area]

    return binary, contours
