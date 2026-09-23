namespace Inspection.Hmi.Services;

/// <summary>
/// 검사할 이미지를 공급한다.
/// 지금은 파일(FileImageSource), 나중에 카메라(CameraImageSource)로 교체·전환한다.
/// </summary>
public interface IImageSource
{
    /// <summary>검사할 이미지 한 장 (JPEG 바이트).</summary>
    Task<byte[]> GetImageAsync(CancellationToken ct = default);

    /// <summary>화면에 표시할 현재 입력 설명 (예: "파일: board.jpg").</summary>
    string Description { get; }
}