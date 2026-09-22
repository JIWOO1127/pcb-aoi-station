using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Inspection.Contracts;
using Inspection.Hmi.Services;

namespace Inspection.Hmi.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStationClient _client;

    public MainViewModel(IStationClient client) => _client = client;

    [ObservableProperty] private string selectedProduct = "NE555-A";
    [ObservableProperty] private Verdict? verdict;          // null = 대기
    [ObservableProperty] private int defectCount;
    [ObservableProperty] private int latencyMs;
    [ObservableProperty] private string message = "대기 중 — Space를 눌러 검사";

    // 화면 목록에 보여줄 부품별 결과
    public ObservableCollection<PartResult> Parts { get; } = new();

    // [RelayCommand]가 InspectCommand를 자동 생성. 비동기라 실행 중엔 버튼 비활성화.
    [RelayCommand]
    private async Task InspectAsync()
    {
        Message = "검사 중...";
        try
        {
            var r = await _client.TriggerAsync(SelectedProduct);

            Verdict = r.Verdict;
            DefectCount = r.DefectCount;
            LatencyMs = r.LatencyMs;

            Parts.Clear();
            foreach (var part in r.Parts) Parts.Add(part);

            Message = r.Verdict switch
            {
                Inspection.Contracts.Verdict.Pass => "양품",
                Inspection.Contracts.Verdict.Fail =>
                    "불량: " + string.Join(", ", r.Parts.Where(p => p.Defect is not null).Select(Describe))
                    + " — 확인 후 불량함으로",
                _ => ToErrorMessage(r.ErrorCode)
            };
        }
        catch (Exception ex)
        {
            Verdict = null;
            Message = $"통신 오류: {ex.Message}";
        }
    }

    // 불량을 작업자가 바로 이해할 문장으로
    private static string Describe(PartResult p) => p.Defect switch
    {
        DefectType.Missing => $"{p.PartId} 누락",
        DefectType.WrongPart => $"{p.PartId} 오부품({p.ExpectedClass}→{p.DetectedClass})",
        DefectType.WireColorSwap => $"{p.PartId} 전선 색",
        DefectType.Offset => $"{p.PartId} 위치 어긋남",
        DefectType.Polarity => $"{p.PartId} IC 방향",
        DefectType.Extra => $"{p.PartId} 추가 부품",
        _ => p.PartId
    };

    private static string ToErrorMessage(string? code) => code switch
    {
        "MARKER_NOT_FOUND" => "기준 마크를 찾지 못했습니다 — 보드 위치를 확인하세요",
        "CAPTURE_QUALITY" => "촬영 품질 미달 — 조명·초점 확인 후 재검사",
        _ => "검사 불가"
    };
}