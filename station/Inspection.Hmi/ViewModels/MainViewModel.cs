using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Inspection.Contracts;
using Inspection.Hmi.Services;
using Microsoft.Win32;

namespace Inspection.Hmi.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStationClient _client;
    private readonly IImageSource _imageSource;

    public MainViewModel(IStationClient client, IImageSource imageSource)
    {
        _client = client;
        _imageSource = imageSource;
        ImageDescription = imageSource.Description;
    }

    [ObservableProperty] private string selectedProduct = "NE555-A";
    [ObservableProperty] private Verdict? verdict;          // null = 대기
    [ObservableProperty] private int defectCount;
    [ObservableProperty] private int latencyMs;
    [ObservableProperty] private string message = "대기 중 — Space를 눌러 검사";
    [ObservableProperty] private string imageDescription = "";

    public ObservableCollection<PartResult> Parts { get; } = new();

    /// <summary>검사 실행. 이미지 공급처(파일 또는 카메라)에서 사진을 받아 스테이션에 보낸다.</summary>
    [RelayCommand]
    private async Task InspectAsync()
    {
        Message = "검사 중...";
        try
        {
            var image = await _imageSource.GetImageAsync();
            var r = await _client.TriggerAsync(image, SelectedProduct);

            Verdict = r.Verdict;
            DefectCount = r.DefectCount;
            LatencyMs = r.LatencyMs;

            Parts.Clear();
            foreach (var part in r.Parts) Parts.Add(part);

            Message = r.Verdict switch
            {
                Inspection.Contracts.Verdict.Pass => "양품",
                Inspection.Contracts.Verdict.Fail =>
                    "불량: " + string.Join(", ",
                        r.Parts.Where(p => p.Defect is not null).Select(Describe))
                    + " — 확인 후 불량함으로",
                _ => ToErrorMessage(r.ErrorCode)
            };
        }
        catch (Exception ex)
        {
            Verdict = null;
            Message = $"오류: {ex.Message}";
        }
    }

    /// <summary>검사할 사진을 고른다. 카메라가 붙은 뒤에도 재현·테스트용으로 남겨둔다.</summary>
    [RelayCommand]
    private void OpenImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "검사할 사진 선택",
            Filter = "이미지 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|모든 파일 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        if (_imageSource is FileImageSource file)
        {
            file.SetPath(dialog.FileName);
            ImageDescription = file.Description;
            Message = "사진을 바꿨습니다 — Space를 눌러 검사";
        }
    }

    private static string Describe(PartResult p) => p.Defect switch
    {
        DefectType.Missing => $"{p.PartId} 누락",
        DefectType.WrongPart => $"{p.PartId} 오부품({p.ExpectedClass}→{p.DetectedClass})",
        DefectType.WireColorSwap => $"{p.PartId} 전선 색",
        DefectType.Offset => $"{p.PartId} 위치 어긋남",
        DefectType.Extra => $"{p.PartId} 추가 부품",
        _ => p.PartId
    };

    private static string ToErrorMessage(string? code) => code switch
    {
        "MARKER_NOT_FOUND" => "기준 마크를 찾지 못했습니다 — 보드 위치를 확인하세요",
        "CAPTURE_QUALITY" => "촬영 품질 미달 — 조명·초점 확인 후 재검사",
        "INFERENCE_UNAVAILABLE" => "추론 서비스에 연결할 수 없습니다 — Python 서버를 확인하세요",
        _ => "검사 불가"
    };
}