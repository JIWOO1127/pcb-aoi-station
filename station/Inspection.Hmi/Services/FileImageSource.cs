using System.IO;
using Microsoft.Extensions.Configuration;

namespace Inspection.Hmi.Services;

/// <summary>
/// 파일에서 이미지를 읽는다.
/// 기본 경로는 appsettings.json의 TestImage:Path, 작업자가 "사진 열기"로 바꿀 수 있다.
/// </summary>
public class FileImageSource : IImageSource
{
    public FileImageSource(IConfiguration config)
    {
        Path = config["TestImage:Path"];
    }

    /// <summary>현재 선택된 사진 경로 (미리보기 표시에 쓴다).</summary>
    public string? Path { get; private set; }

    /// <summary>작업자가 다른 사진을 골랐을 때.</summary>
    public void SetPath(string path) => Path = path;

    public string Description =>
        Path is null ? "사진 없음" : $"파일: {System.IO.Path.GetFileName(Path)}";

    public async Task<byte[]> GetImageAsync(CancellationToken ct = default)
    {
        if (Path is null)
            throw new InvalidOperationException("검사할 사진이 지정되지 않았습니다.");
        if (!File.Exists(Path))
            throw new FileNotFoundException($"사진 파일이 없습니다: {Path}");

        return await File.ReadAllBytesAsync(Path, ct);
    }
}
