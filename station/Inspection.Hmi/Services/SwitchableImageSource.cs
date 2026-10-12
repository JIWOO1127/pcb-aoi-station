using Microsoft.Extensions.Configuration;

namespace Inspection.Hmi.Services;

public enum InputMode { Camera, File }

/// <summary>
/// 검사 입력을 카메라 ↔ 사진 파일 사이에서 전환한다.
/// 현장 운용은 카메라, 카메라가 안 될 때(발표장 등)는 사진 파일로 바로 대체할 수 있게.
/// 시작 모드는 appsettings.json의 Camera:Enabled (기본 true = 카메라).
/// </summary>
public sealed class SwitchableImageSource : IImageSource
{
    public SwitchableImageSource(CameraImageSource camera, FileImageSource file,
                                 IConfiguration config)
    {
        Camera = camera;
        File = file;
        Mode = config.GetValue("Camera:Enabled", true) ? InputMode.Camera : InputMode.File;
    }

    public CameraImageSource Camera { get; }
    public FileImageSource File { get; }
    public InputMode Mode { get; set; }

    public string Description => Mode == InputMode.Camera ? Camera.Description : File.Description;

    public Task<byte[]> GetImageAsync(CancellationToken ct = default)
        => Mode == InputMode.Camera ? Camera.GetImageAsync(ct) : File.GetImageAsync(ct);
}
