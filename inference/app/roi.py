"""고정 카메라·지그 세팅에서, 보드가 대략 나오는 영역(ROI)을 잘라낸다.

카메라와 촬영 지그가 고정되어 있어서 들어오는 이미지의 해상도·프레임은
항상 같다. 그래서 보드가 대강 어디 나오는지도 고정된 사각형(ROI)으로
미리 잘라낼 수 있다.

(홀 픽셀 위치가 아주 조금씩 달라지는 미세한 부분은 이 크롭과는 별개로
calibration.py가 크롭된 이미지 안에서 따로 보정한다. 이 모듈은 "대략
보드가 있는 영역만 남기고 나머지는 잘라내는" 역할만 한다.)

아직 없는 것 (TODO):
- 실제 카메라로 촬영해서 ROI 좌표를 채워야 한다. 아직 실측 전이라
  자리표시자(None)로 남겨둠 — 이 상태로 crop_to_roi를 호출하면 에러가 난다.
"""

import numpy as np

# (x1, y1, x2, y2) 픽셀 좌표. 원본 이미지에서 보드가 있는 대략적인 영역.
ROI: tuple[int, int, int, int] | None = None


def crop_to_roi(image: np.ndarray) -> np.ndarray:
    """디코딩된 원본 이미지를 고정 ROI로 잘라낸다.

    image: cv2로 디코딩한 BGR 배열 (H, W, 3)
    """
    if ROI is None:
        raise NotImplementedError(
            "ROI 좌표가 아직 설정되지 않았습니다. 실제 촬영 이미지로 값을 채운 뒤 사용하세요."
        )
    x1, y1, x2, y2 = ROI
    return image[y1:y2, x1:x2]
