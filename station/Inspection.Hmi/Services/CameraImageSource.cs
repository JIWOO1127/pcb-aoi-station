using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Configuration;
using OpenCvSharp;

namespace Inspection.Hmi.Services;

/// <summary>
/// 카메라(Camo 가상 웹캠 등)에서 프레임을 계속 받아두고,
/// 검사할 때는 그 순간의 최신 프레임 한 장을 JPEG로 넘긴다.
///
/// - 백그라운드 스레드가 계속 읽는다 (화면이 멈추지 않게)
/// - 미리보기 이미지는 초당 PreviewFps장만 만든다 (CPU 절약)
/// - 프레임은 돌리지 않고 원본 그대로 보낸다. 방향은 추론 서비스(roi.py)가
///   4방향을 시도해 스스로 맞춘다. 양쪽에서 돌리면 보드가 뒤집힌다.
/// </summary>
public sealed class CameraImageSource : IImageSource, IDisposable
{
    private readonly object _lock = new();
    private readonly int _requestWidth;
    private readonly int _requestHeight;
    private readonly int _previewIntervalMs;

    private Mat? _latest;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public CameraImageSource(IConfiguration config)
    {
        Index = config.GetValue("Camera:Index", 1);
        _requestWidth = config.GetValue("Camera:Width", 1920);
        _requestHeight = config.GetValue("Camera:Height", 1080);
        _previewIntervalMs = 1000 / Math.Max(1, config.GetValue("Camera:PreviewFps", 15));
    }

    /// <summary>카메라 번호. 0은 보통 노트북 내장 카메라, Camo는 1이나 2인 경우가 많다.</summary>
    public int Index { get; private set; }
    public bool IsConnected { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }

    /// <summary>미리보기 프레임. 백그라운드 스레드에서 호출되며, 이미지는 Freeze되어 있어 UI 스레드에서 바로 쓸 수 있다.</summary>
    public event Action<BitmapSource>? FrameArrived;

    /// <summary>연결 상태가 바뀔 때. 백그라운드 스레드에서 호출된다.</summary>
    public event Action<bool>? ConnectionChanged;

    public string Description => IsConnected
        ? $"카메라 #{Index} ({FrameWidth}x{FrameHeight})"
        : $"카메라 #{Index} (연결 안 됨)";

    /// <summary>카메라를 연다. index를 주면 그 번호로 바꿔서 연다.</summary>
    public void Start(int? index = null)
    {
        Stop();
        if (index is not null) Index = index.Value;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Factory.StartNew(() => CaptureLoop(token), token,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _loop?.Wait(1500); } catch { /* 종료 중 예외는 무시 */ }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        SetConnected(false);
    }

    private void CaptureLoop(CancellationToken token)
    {
        // DSHOW: 윈도우에서 가상 웹캠(Camo)을 가장 안정적으로 여는 방식
        using var capture = new VideoCapture(Index, VideoCaptureAPIs.DSHOW);

        // 없는 번호를 여는 데 몇 초 걸릴 수 있다. 그 사이 [카메라 전환]을 또 눌렀으면
        // 이 루프는 이미 버려진 것이므로 상태를 건드리지 않고 조용히 끝낸다.
        if (token.IsCancellationRequested) return;
        if (!capture.IsOpened())
        {
            SetConnected(false);
            return;
        }

        // 요청값일 뿐이다. 카메라가 지원하는 가장 가까운 해상도로 열린다.
        capture.Set(VideoCaptureProperties.FrameWidth, _requestWidth);
        capture.Set(VideoCaptureProperties.FrameHeight, _requestHeight);

        using var frame = new Mat();
        int failures = 0;
        var lastPreview = DateTime.MinValue;

        while (!token.IsCancellationRequested)
        {
            if (!capture.Read(frame) || frame.Empty())
            {
                // 잠깐 끊기는 건 버티고, 계속 실패하면 끊긴 것으로 본다
                if (++failures > 30 && !token.IsCancellationRequested) SetConnected(false);
                Thread.Sleep(50);
                continue;
            }

            failures = 0;
            FrameWidth = frame.Width;
            FrameHeight = frame.Height;
            SetConnected(true);

            lock (_lock)
            {
                _latest?.Dispose();
                _latest = frame.Clone();
            }

            if ((DateTime.UtcNow - lastPreview).TotalMilliseconds >= _previewIntervalMs)
            {
                lastPreview = DateTime.UtcNow;
                FrameArrived?.Invoke(ToBitmap(frame));
            }
        }
    }

    /// <summary>검사할 때 호출. 지금 이 순간의 최신 프레임을 JPEG로 준다.</summary>
    public Task<byte[]> GetImageAsync(CancellationToken ct = default)
    {
        Mat snapshot;
        lock (_lock)
        {
            if (_latest is null)
                throw new InvalidOperationException("카메라 프레임이 없습니다. 카메라 연결을 확인하세요.");
            snapshot = _latest.Clone();
        }

        using (snapshot)
        {
            Cv2.ImEncode(".jpg", snapshot, out var bytes,
                new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
            return Task.FromResult(bytes);
        }
    }

    /// <summary>OpenCV 프레임(BGR)을 WPF 이미지로. Freeze해서 다른 스레드에서도 쓸 수 있게 한다.</summary>
    private static BitmapSource ToBitmap(Mat bgr)
    {
        using var continuous = bgr.Clone();   // Clone은 메모리가 연속인 Mat을 만든다
        int stride = (int)continuous.Step();
        var bmp = BitmapSource.Create(continuous.Width, continuous.Height, 96, 96,
            PixelFormats.Bgr24, null, continuous.Data, stride * continuous.Height, stride);
        bmp.Freeze();
        return bmp;
    }

    private void SetConnected(bool value)
    {
        if (IsConnected == value) return;
        IsConnected = value;
        ConnectionChanged?.Invoke(value);
    }

    public void Dispose()
    {
        Stop();
        lock (_lock)
        {
            _latest?.Dispose();
            _latest = null;
        }
    }
}
