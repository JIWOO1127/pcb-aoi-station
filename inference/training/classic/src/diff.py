import cv2


def compute_diff(golden_img, aligned_img, blur_ksize=5):
    """정렬된 두 이미지(golden, aligned input)의 그레이스케일 절대 차이를 계산.

    blur_ksize: 0이면 블러 생략, 그 외엔 GaussianBlur 커널 크기(홀수).
                센서 노이즈/압축 잡음으로 인한 미세 diff를 줄여준다.
    """
    gray_golden = cv2.cvtColor(golden_img, cv2.COLOR_BGR2GRAY) if golden_img.ndim == 3 else golden_img
    gray_input = cv2.cvtColor(aligned_img, cv2.COLOR_BGR2GRAY) if aligned_img.ndim == 3 else aligned_img

    if blur_ksize > 0:
        gray_golden = cv2.GaussianBlur(gray_golden, (blur_ksize, blur_ksize), 0)
        gray_input = cv2.GaussianBlur(gray_input, (blur_ksize, blur_ksize), 0)

    return cv2.absdiff(gray_golden, gray_input)
