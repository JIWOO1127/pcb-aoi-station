using System.IO;
using Microsoft.Extensions.Configuration;

namespace Inspection.Hmi.Services;

/// <summary>
/// 파일에서 이미지를 읽는다.
/// 기본 경로는 appsettings.json의 TestImage:Path, 작업자가 "사진 열기"로 바꿀 수 있다.
/// </summary>
public class FileImageSource : IImageSource
{
    private string? _path;

    public FileImageSource(IConfiguration config)
    {
        _path = config["TestImage:Path"];
    }

    /// <summary>작업자가 다른 사진을 골랐을 때.</summary>
    public void SetPath(string path) => _path = path;

    public string Description =>
        _path is null ? "사진 없음" : $"파일: {Path.GetFileName(_path)}";

    public async Task<byte[]> GetImageAsync(CancellationToken ct = default)
    {
        if (_path is null)
            throw new InvalidOperationException("검사할 사진이 지정되지 않았습니다.");
        if (!File.Exists(_path))
            throw new FileNotFoundException($"사진 파일이 없습니다: {_path}");

        return await File.ReadAllBytesAsync(_path, ct);
    }
}