namespace Inspection.Contracts;

/// <summary>
/// 결과 화면을 실제 사진 위에 그리기 위한 정보. 추론 서비스의 /infer?overlay=true 응답을 그대로 옮긴다.
/// 좌표는 모두 "방향을 맞추고 잘라낸(정렬된) 사진"의 좌상단 기준 픽셀이다.
/// 추론 서비스가 이 정보를 주지 않으면 null이고, HMI는 보드 미니어처로 대신 그린다.
/// </summary>
/// <param name="Width">정렬된 사진 가로(px)</param>
/// <param name="Height">정렬된 사진 세로(px)</param>
/// <param name="ImageJpeg">정렬된 사진 JPEG, base64</param>
/// <param name="Pinholes">구멍 282개의 사진 속 위치 (부품에 가려진 구멍도 계산된 위치가 있다)</param>
/// <param name="Boxes">YOLO가 찾은 부품 박스 ("모델이 본 것" 보기용)</param>
public record InspectionOverlay(int Width, int Height, string ImageJpeg,
    List<OverlayPinhole> Pinholes, List<OverlayBox> Boxes);

public record OverlayPinhole(string Pinhole, double X, double Y);

public record OverlayBox(string Class, double Confidence, double X1, double Y1, double X2, double Y2);
