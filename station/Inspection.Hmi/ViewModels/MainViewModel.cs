using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Inspection.Contracts;
using Inspection.Hmi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace Inspection.Hmi.ViewModels;

/// <summary>
/// 왼쪽 정사각형 패널에 무엇을 보여주는가.
/// 검사 영역이 거의 정사각형이라, 카메라 화면·검사한 사진·보드 표시를 모두 같은 정사각형 자리에 번갈아 띄운다.
/// </summary>
public enum PanelMode
{
    Live,       // 실시간 (가이드 주변만 잘라서) — 보드를 놓을 때
    Captured,   // 방금 검사한 사진 (같은 영역을 잘라서) — 검사 중, 판정 불가일 때
    Board       // 보드 미니어처 + 동작 표시 — PASS, FAIL일 때
}

/// <summary>로그인 직후 헤더 아래 레시피 배너 종류.</summary>
public enum RecipeBannerKind
{
    None,
    Updated,   // 파란색: MES에서 새 승인 레시피를 받음
    Offline    // 노란색: MES에 닿지 못해 마지막으로 받은 레시피로 검사
}

/// <summary>
/// 작업자 화면의 상태와 동작.
///
/// 공정 전제: FAIL이면 작업자가 그 자리에서 수리하고 재검사한다.
/// 수리가 불가능하면 불량 확정으로 빼낸다.
///
/// 상태 전이:
///   Idle ─검사→ Busy ─→ Pass ─(자동 N초)→ Idle
///                    ─→ Fail ─재검사→ Busy (같은 보드, #0007-2) / ─불량확정→ Idle
///                    ─→ Invalid ─재검사→ Busy (같은 보드)
///
/// v13에서 바뀐 것:
///   1. 왼쪽 정사각형 패널 하나로 합침 (PanelMode)
///   2. 재검사 비교: 같은 보드 번호 유지, "1건 해결 · 남은 불량 1건 · 새 불량 1건"
///   3. 재검사 횟수 제한 안내 (기본 3회)
///   4. PASS 후 자동으로 대기 화면 복귀 (기본 2.5초)
///   5. "직전" 표시가 진짜 직전 결과를 보여주도록 수정 (전에는 현재 결과가 나왔다)
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IStationClient _client;
    private readonly SwitchableImageSource _input;
    private readonly WorkerSession _session;
    private readonly bool _requireLogin;     // true면 로그인해야 검사할 수 있다 (Hmi:RequireLogin)
    private readonly Dispatcher _ui;
    private readonly DispatcherTimer _passTimer;
    private readonly DispatcherTimer _mesTimer;   // 5초마다 스테이션에 MES 전송 상태를 묻는다
    private bool _mesRefreshing;                  // 이전 조회가 끝나기 전에 다시 부르지 않게
    private readonly DispatcherTimer _recipeBannerTimer;   // 로그인 직후 레시피 배너를 5초 뒤 내린다

    private readonly int _maxCameraIndex;
    private readonly double _guideAspect;   // 검사 영역의 가로/세로 (화면에 보이는 방향 기준)
    private readonly double _guideFill;     // 가이드가 화면의 몇 %를 채울지
    private readonly int _maxAttempts;      // 이 횟수째 재검사에서도 FAIL이면 불량 확정을 권한다

    /// <summary>잘라서 보여줄 때 가이드 바깥으로 남기는 여백 (가이드 크기 대비). 보드가 삐져나간 걸 보이게.</summary>
    public const double CropMargin = 0.05;

    private int _previewPending;            // 미리보기가 UI에 밀려 쌓이지 않게 (0/1)

    // 보드 번호와 재검사 추적
    private int _boardNo;                   // 보드마다 1씩 증가 (#0007)
    private int _attempt;                   // 같은 보드에서 몇 번째 검사인지 (#0007-2)
    private List<PartResult>? _prevDefects; // 같은 보드의 직전 FAIL 불량 (재검사 비교용)
    private string _lastSummary = "";       // 다음 검사 때 "직전 ..."으로 보여줄 요약

    public MainViewModel(IStationClient client, SwitchableImageSource input, WorkerSession session,
        IConfiguration config)
    {
        _client = client;
        _input = input;
        _session = session;
        _requireLogin = config.GetValue("Hmi:RequireLogin", true);
        _ui = Application.Current.Dispatcher;

        _maxCameraIndex = config.GetValue("Camera:MaxIndex", 4);
        _guideAspect = config.GetValue("Camera:GuideAspect", 0.97);
        _guideFill = config.GetValue("Camera:GuideFill", 0.9);
        ShowFrameGuide = config.GetValue("Camera:ShowGuide", true);
        _maxAttempts = config.GetValue("Hmi:MaxAttempts", 3);

        // 헤더 드롭다운에 보일 제품 (Hmi:Products). 첫 번째가 기본 선택. 없으면 NE555-A 하나.
        var products = config.GetSection("Hmi:Products").Get<string[]>();
        Products = products is { Length: > 0 } ? products : ["NE555-A"];

        _passTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(config.GetValue("Hmi:PassHoldSeconds", 2.5))
        };
        _passTimer.Tick += (_, _) =>
        {
            _passTimer.Stop();
            if (State == ScreenState.Pass) GoIdle();
        };

        _mesTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _mesTimer.Tick += async (_, _) => await RefreshMesStatusAsync();
        _mesTimer.Start();
        _ = RefreshMesStatusAsync();   // 5초 기다리지 않고 바로 한 번

        _recipeBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _recipeBannerTimer.Tick += (_, _) =>
        {
            _recipeBannerTimer.Stop();
            RecipeBannerKind = RecipeBannerKind.None;
        };

        _input.Camera.FrameArrived += OnFrameArrived;
        _input.Camera.ConnectionChanged += OnCameraConnectionChanged;

        BuildEmptyBoard();
        RefreshInputInfo();
        GoIdle();
        SelectedProduct = Products[0];   // 기본 선택 (NE555-A가 아니면 위 초기화가 한 번 더 돈다 — 무해)

        if (_input.Mode == InputMode.Camera) _input.Camera.Start();
        else LoadFilePreview();
    }

    // ── 상태 ────────────────────────────────────────────────
    [ObservableProperty] private ScreenState state = ScreenState.Idle;

    /// <summary>선택할 수 있는 제품 (레시피 이름과 같다: Station Recipes/{제품}.json).</summary>
    public IReadOnlyList<string> Products { get; }

    [ObservableProperty] private string selectedProduct = "NE555-A";

    /// <summary>검사 중에는 제품을 바꿀 수 없다.</summary>
    public bool CanChangeProduct => State != ScreenState.Busy;

    /// <summary>
    /// 제품을 바꾸면 다른 보드이므로 재검사 흐름을 끊는다:
    /// 보드 번호를 처음부터, 직전 불량·직전 결과 요약을 지우고 대기 화면으로.
    /// </summary>
    partial void OnSelectedProductChanged(string value)
    {
        _boardNo = 0;
        _attempt = 0;
        _prevDefects = null;
        _lastSummary = "";
        LastResultNote = "";
        GoIdle();
    }
    [ObservableProperty] private string bannerText = "보드를 올리세요";
    [ObservableProperty] private string bannerNote = "";       // 배너 오른쪽 아래 작은 글씨 (재검사 비교)
    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private string inspectionStamp = "";  // #0007-2  12:31:08
    [ObservableProperty] private string lastResultNote = "";   // 직전 #0007 FAIL 2건
    [ObservableProperty] private string imageDescription = "";
    [ObservableProperty] private int latencyMs;
    [ObservableProperty] private PartResult? selectedPart;

    // ── 로그인 ──────────────────────────────────────────────
    [ObservableProperty] private string loginEmployeeId = "";
    [ObservableProperty] private string loginMessage = "";
    [ObservableProperty] private LoginResult? currentWorker;
    [ObservableProperty] private bool isLoggingIn;

    public bool IsLoggedIn => CurrentWorker is not null;

    /// <summary>로그인 화면을 덮어 보여줄지. 로그인하지 않아도 되는 설정이면 보이지 않는다.</summary>
    public bool ShowLoginOverlay => _requireLogin && !IsLoggedIn;

    /// <summary>헤더 표시: "작업자 홍길동 (A-1042)", MES가 꺼진 채 로그인했으면 "· 오프라인"</summary>
    public string WorkerText => CurrentWorker is { } w
        ? $"작업자 {w.Name} ({w.EmployeeId}){(w.Offline ? " · 오프라인" : "")}"
        : "";

    // ── 로그인 직후 레시피 배너 (헤더 아래 5초, 검사를 막지 않는다) ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecipeBanner))]
    private RecipeBannerKind recipeBannerKind = RecipeBannerKind.None;
    [ObservableProperty] private string recipeBannerTitle = "";
    [ObservableProperty] private string recipeBannerDetail = "";

    public bool ShowRecipeBanner => RecipeBannerKind != RecipeBannerKind.None;

    /// <summary>
    /// 로그인 응답의 recipeSync 중 지금 선택된 제품 한 줄로 배너를 정한다.
    ///   UPDATED → 파란 배너 "레시피 업데이트", OFFLINE → 노란 배너 "MES 연결 안 됨", 그 밖(UNCHANGED 등)은 없음
    /// </summary>
    private void ShowRecipeSync(IReadOnlyList<RecipeSyncItem>? items)
    {
        _recipeBannerTimer.Stop();
        RecipeBannerKind = RecipeBannerKind.None;

        var item = items?.FirstOrDefault(i => i.ProductId == SelectedProduct);
        if (item is null) return;

        string version = item.Version is { } v ? $"v{v}" : "";
        switch (item.Status)
        {
            case "UPDATED":
                RecipeBannerTitle = $"레시피 업데이트 · {item.ProductId} {version} 적용됨";
                string approvedAt = item.ApprovedAt is { } at ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";
                RecipeBannerDetail = $"승인 {item.ApprovedBy ?? "-"} · {approvedAt} · 이번 검사부터 {version} 기준으로 판정합니다";
                RecipeBannerKind = RecipeBannerKind.Updated;
                break;
            case "OFFLINE":
                RecipeBannerTitle = $"MES 연결 안 됨 · 마지막으로 받은 {item.ProductId} {version}로 검사합니다";
                RecipeBannerDetail = "";
                RecipeBannerKind = RecipeBannerKind.Offline;
                break;
            default:
                return;
        }
        _recipeBannerTimer.Start();
    }

    partial void OnCurrentWorkerChanged(LoginResult? value)
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(ShowLoginOverlay));
        OnPropertyChanged(nameof(WorkerText));
        InspectCommand.NotifyCanExecuteChanged();
        LogoutCommand.NotifyCanExecuteChanged();
    }

    // ── 실제 사진 위 표시 (추론 서비스가 overlay를 줄 때) ─────
    /// <summary>사진 표시 좌표계의 가로 폭. 사진 크기와 상관없이 선 두께·글자 크기가 같아 보이게 이 폭으로 맞춘다.</summary>
    public const double PhotoViewWidth = 400;

    [ObservableProperty] private BitmapSource? overlayImage;      // 정렬된 실제 사진
    [ObservableProperty] private double overlayViewHeight = 400;  // PhotoViewWidth에 맞춘 세로
    [ObservableProperty] private bool preferPhoto = true;         // 사진 / 배치도 전환
    [ObservableProperty] private bool showModelBoxes;             // "모델이 본 것"

    public ObservableCollection<BoardPart> OverlayParts { get; } = new();
    public ObservableCollection<ModelBox> ModelBoxes { get; } = new();

    public bool HasOverlay => OverlayImage is not null;
    public bool ShowPhoto => Panel == PanelMode.Board && HasOverlay && PreferPhoto;
    public bool ShowMiniature => Panel == PanelMode.Board && !(HasOverlay && PreferPhoto);
    public bool ShowViewToggle => Panel == PanelMode.Board && HasOverlay;
    public string ViewToggleText => PreferPhoto ? "배치도 보기" : "사진 보기";

    partial void OnOverlayImageChanged(BitmapSource? value) => RaisePanelChanged();
    partial void OnPreferPhotoChanged(bool value) => RaisePanelChanged();

    [RelayCommand]
    private void ToggleView() => PreferPhoto = !PreferPhoto;

    [RelayCommand]
    private void ToggleModelBoxes() => ShowModelBoxes = !ShowModelBoxes;

    private void ClearOverlay()
    {
        OverlayImage = null;
        OverlayParts.Clear();
        ModelBoxes.Clear();
    }

    /// <summary>
    /// 추론 서비스가 준 정렬 사진과 구멍 좌표로, 미니어처와 같은 불량 표시를 사진 위에 만든다.
    /// 누락 부품은 YOLO 박스가 없지만, 구멍 좌표로 그리므로 "여기에 꽂으세요" 자리가 정확히 나온다.
    /// </summary>
    private void BuildOverlay(InspectionResult r)
    {
        ClearOverlay();
        if (r.Overlay is not { } o || o.Width <= 0 || string.IsNullOrEmpty(o.ImageJpeg)) return;

        try
        {
            OverlayImage = Decode(Convert.FromBase64String(o.ImageJpeg));
        }
        catch
        {
            return;   // 사진이 깨졌으면 미니어처로
        }

        double k = PhotoViewWidth / o.Width;            // 사진 픽셀 → 표시 좌표
        OverlayViewHeight = o.Height * k;

        var map = o.Pinholes.ToDictionary(p => p.Pinhole, p => (X: p.X * k, Y: p.Y * k));
        (double X, double Y)? Locate(Hole h) => map.TryGetValue(h.ToString(), out var v) ? v : null;

        // 구멍 간격: 같은 열의 이웃한 두 행(B2, C2) 사이 거리
        double pitch = map.TryGetValue("B2", out var b) && map.TryGetValue("C2", out var c)
            ? Math.Abs(c.Y - b.Y) : 12;
        double pad = pitch * 0.55;

        int defectIndex = 0;
        foreach (var part in r.Parts.OrderBy(p => p.Defect is null))
        {
            if (part.Defect is null) continue;   // 사진에는 불량만 그린다 (정상 부품은 사진 그대로 보인다)
            var shape = PhotoLayout.From(part, defectIndex++, Locate, pad, pitch * 0.6, DescribeDefect, Korean);
            if (shape is not null) OverlayParts.Add(shape);
        }

        foreach (var box in o.Boxes)
        {
            ModelBoxes.Add(new ModelBox(box.X1 * k, box.Y1 * k, (box.X2 - box.X1) * k, (box.Y2 - box.Y1) * k,
                $"{Korean(box.Class)} {box.Confidence:0.00}", BoxColor(box.Class)));
        }
    }

    /// <summary>"모델이 본 것" 박스 색. 저항 330Ω/470Ω은 서로 다른 색으로 구분되게.</summary>
    private static string BoxColor(string cls) => cls switch
    {
        "resistance330" => "#FF9800",
        "resistance470" => "#AB47BC",
        "capacitor" => "#26C6DA",
        "ic_chip" => "#BDBDBD",
        "led" => "#FF5252",
        "red_wire" => "#F44336",
        "black_wire" => "#212121",
        "white_wire" => "#FFFFFF",
        _ => "#FFEB3B"
    };

    public ObservableCollection<PartResult> Parts { get; } = new();
    public ObservableCollection<BoardCell> BoardCells { get; } = new();
    public ObservableCollection<BoardPart> BoardParts { get; } = new();
    public ObservableCollection<DefectCard> DefectCards { get; } = new();

    public double BoardWidth => BoardLayout.TotalWidth;
    public double BoardHeight => BoardLayout.TotalHeight;

    /// <summary>상태가 바뀌면 버튼 문구·활성 여부·패널 내용도 함께 갱신한다.</summary>
    partial void OnStateChanged(ScreenState value)
    {
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(ShowRejectButton));
        OnPropertyChanged(nameof(CanChangeProduct));
        InspectCommand.NotifyCanExecuteChanged();
        RejectCommand.NotifyCanExecuteChanged();
        NextCameraCommand.NotifyCanExecuteChanged();
        UseCameraCommand.NotifyCanExecuteChanged();
        OpenImageCommand.NotifyCanExecuteChanged();
        LogoutCommand.NotifyCanExecuteChanged();
        RaisePanelChanged();
    }

    public string PrimaryButtonText => State switch
    {
        ScreenState.Fail => "수리 후 재검사 (Space)",
        ScreenState.Invalid or ScreenState.Error => "재검사 (Space)",
        _ => "검사 (Space)"
    };

    public bool ShowRejectButton => State == ScreenState.Fail;

    // ── 왼쪽 정사각형 패널 ───────────────────────────────────
    [ObservableProperty] private BitmapSource? liveImage;       // 실시간 (카메라: 가이드 주변만 잘라낸 것)
    [ObservableProperty] private BitmapSource? capturedImage;   // 검사한 사진 (카메라: 같은 영역을 잘라낸 것)
    [ObservableProperty] private BitmapSource? capturedFull;    // 검사한 사진 원본 전체 (작은 창용)
    [ObservableProperty] private bool? cameraOk;                // null 회색 / true 초록 / false 빨강
    [ObservableProperty] private bool? inferenceOk;             // 마지막 검사 결과로 추정
    [ObservableProperty] private bool? mesOk;                   // null 회색(꺼짐·모름) / true 초록 / false 빨강
    [ObservableProperty] private string mesText = "MES";        // 못 보낸 기록이 있으면 "MES 대기 N건"

    /// <summary>
    /// 헤더 MES 점 갱신. 스테이션 GET /api/mes/status (2초 제한, 실패하면 null).
    ///   null 또는 전송 꺼짐       → 회색 "MES"
    ///   연결됨 + 대기 0건         → 초록 "MES"
    ///   그 외 (연결 실패·대기 있음) → 빨강, 대기가 있으면 "MES 대기 N건"
    /// </summary>
    private async Task RefreshMesStatusAsync()
    {
        if (_mesRefreshing) return;
        _mesRefreshing = true;
        try
        {
            var s = await _client.GetMesStatusAsync();
            if (s is null || !s.Enabled)
            {
                MesOk = null;
                MesText = "MES";
            }
            else if (s.Connected is null && s.Pending == 0)
            {
                // 스테이션이 켜진 뒤 아직 한 번도 보내지 않음 → 모름(회색)
                MesOk = null;
                MesText = "MES";
            }
            else if (s.Connected == true && s.Pending == 0)
            {
                MesOk = true;
                MesText = "MES";
            }
            else
            {
                MesOk = false;
                MesText = s.Pending > 0 ? $"MES 대기 {s.Pending}건" : "MES";
            }
        }
        finally
        {
            _mesRefreshing = false;
        }
    }
    [ObservableProperty] private bool isCameraMode;
    [ObservableProperty] private bool showFrameGuide;

    public PanelMode Panel => State switch
    {
        ScreenState.Pass or ScreenState.Fail => PanelMode.Board,
        ScreenState.Busy or ScreenState.Invalid or ScreenState.Error when CapturedImage is not null => PanelMode.Captured,
        _ => PanelMode.Live
    };

    public BitmapSource? PanelImage => Panel switch
    {
        PanelMode.Captured => CapturedImage,
        PanelMode.Live => LiveImage,
        _ => null
    };

    public bool ShowBoard => Panel == PanelMode.Board;
    public bool ShowPanelImage => Panel != PanelMode.Board && PanelImage is not null;
    public bool ShowNoImage => Panel != PanelMode.Board && PanelImage is null;

    /// <summary>가이드는 카메라 실시간일 때만. 사진 파일은 구도가 제각각이라 의미가 없다.</summary>
    public bool ShowGuideNow => ShowFrameGuide && IsCameraMode && Panel == PanelMode.Live;

    /// <summary>작은 창: 보드 표시 중이면 촬영 원본을 보여준다 (보드가 제대로 찍혔는지 확인용).</summary>
    public bool ShowInset => Panel == PanelMode.Board && CapturedFull is not null;

    public string PanelLabel => Panel switch
    {
        PanelMode.Board => ShowPhoto ? "검사한 사진 (정렬됨)" : "보드 배치",
        PanelMode.Captured => "검사한 사진",
        _ => IsCameraMode ? "실시간" : "선택한 사진"
    };

    public string NoImageText => IsCameraMode
        ? (CameraOk == false ? $"카메라 #{_input.Camera.Index} 연결 안 됨\n[카메라 전환]으로 번호를 바꿔보세요"
                             : "카메라 연결 중...")
        : "[사진 열기]로 검사할 사진을 고르세요";

    private void RaisePanelChanged()
    {
        foreach (var name in new[]
                 {
                     nameof(Panel), nameof(PanelImage), nameof(ShowBoard), nameof(ShowPanelImage),
                     nameof(ShowNoImage), nameof(ShowGuideNow), nameof(ShowInset), nameof(PanelLabel),
                     nameof(NoImageText), nameof(HasOverlay), nameof(ShowPhoto), nameof(ShowMiniature),
                     nameof(ShowViewToggle), nameof(ViewToggleText)
                 })
            OnPropertyChanged(name);
    }

    partial void OnLiveImageChanged(BitmapSource? value) => RaisePanelChanged();
    partial void OnCapturedImageChanged(BitmapSource? value) => RaisePanelChanged();
    partial void OnCapturedFullChanged(BitmapSource? value) => RaisePanelChanged();
    partial void OnCameraOkChanged(bool? value) => RaisePanelChanged();
    partial void OnIsCameraModeChanged(bool value) => RaisePanelChanged();

    private void BuildEmptyBoard()
    {
        foreach (var hole in BoardLayout.AllHoles())
        {
            BoardCells.Add(new BoardCell(
                X: BoardLayout.X(hole.Col) - BoardLayout.HoleSize / 2,
                Y: BoardLayout.Y(hole.Row) - BoardLayout.HoleSize / 2,
                Size: BoardLayout.HoleSize,
                Label: hole.ToString()));
        }
    }

    /// <summary>대기 상태로. 이전 결과를 지워 새 보드의 판정으로 오해되지 않게 한다.</summary>
    private void GoIdle()
    {
        _passTimer.Stop();
        State = ScreenState.Idle;
        BannerText = "보드를 올리세요";
        BannerNote = "";
        StatusMessage = IsCameraMode
            ? "노란 점선 안에 보드의 10~30 구간과 양쪽 레일을 꽉 차게 맞추고 Space"
            : "Space를 눌러 선택한 사진을 검사";
        InspectionStamp = "";
        _prevDefects = null;
        Parts.Clear();
        BoardParts.Clear();
        DefectCards.Clear();
        ClearOverlay();
        RaisePanelChanged();
    }

    // ── 검사 ────────────────────────────────────────────────

    private bool CanInspect() => State != ScreenState.Busy && (!_requireLogin || IsLoggedIn);

    [RelayCommand(CanExecute = nameof(CanInspect))]
    private async Task InspectAsync()
    {
        _passTimer.Stop();

        // FAIL·판정불가·오류 뒤의 검사는 "같은 보드를 고친 뒤 다시" 하는 것으로 본다
        bool retest = State is ScreenState.Fail or ScreenState.Invalid or ScreenState.Error;
        if (retest) _attempt++;
        else
        {
            _boardNo++;
            _attempt = 1;
            _prevDefects = null;
        }

        // 직전 결과를 작게 남긴다 (지금 검사가 아니라 바로 앞 검사)
        LastResultNote = _lastSummary.Length > 0 ? $"직전 {_lastSummary}" : "";

        string no = _attempt == 1 ? $"#{_boardNo:D4}" : $"#{_boardNo:D4}-{_attempt}";
        InspectionStamp = $"{no}  {DateTime.Now:HH:mm:ss}";

        ImageDescription = _input.Description;   // "— Space로 재검사" 안내를 지운다
        State = ScreenState.Busy;
        BannerText = retest ? "재검사 중" : "검사 중";
        BannerNote = "";
        StatusMessage = "";
        Parts.Clear();
        BoardParts.Clear();
        DefectCards.Clear();
        ClearOverlay();

        byte[] image;
        try
        {
            // 누른 순간의 프레임 한 장. 이 사진이 그대로 판정되고 화면에도 남는다.
            image = await _input.GetImageAsync();
            var full = Decode(image);
            CapturedFull = full;
            CapturedImage = IsCameraMode ? CropToGuide(full) : full;
        }
        catch (Exception ex)
        {
            State = ScreenState.Error;
            BannerText = IsCameraMode ? "카메라 오류" : "사진 오류";
            StatusMessage = ex.Message;
            _lastSummary = $"{no} 오류";
            return;
        }

        try
        {
            var r = await _client.TriggerAsync(image, SelectedProduct);
            LatencyMs = r.LatencyMs;
            InferenceOk = r.ErrorCode is not ("INFERENCE_UNAVAILABLE" or "INFERENCE_TIMEOUT");
            ShowResult(r, no);
        }
        catch (Exception ex)
        {
            // 스테이션 서버 자체에 닿지 못함
            State = ScreenState.Error;
            BannerText = "연결 오류";
            StatusMessage = $"스테이션 서버 연결을 확인하세요 ({ex.Message})";
            _lastSummary = $"{no} 연결 오류";
        }
    }

    private void ShowResult(InspectionResult r, string no)
    {
        var defects = r.Parts.Where(p => p.Defect is not null).ToList();

        // 재검사 비교: 부품 ID 기준으로 직전 FAIL과 비교한다
        var prevIds = _prevDefects?.Select(p => p.PartId).ToHashSet();
        var nowIds = defects.Select(p => p.PartId).ToHashSet();
        var newIds = prevIds is null ? new HashSet<string>() : nowIds.Except(prevIds).ToHashSet();
        int resolved = prevIds?.Except(nowIds).Count() ?? 0;

        int defectIndex = 0;
        foreach (var part in r.Parts.OrderBy(p => p.Defect is null))   // 불량을 먼저
        {
            Parts.Add(part);
            int badgeIndex = part.Defect is not null ? defectIndex++ : -1;
            var shape = BoardPart.From(part, badgeIndex, DescribeDefect, Korean);
            if (shape is not null) BoardParts.Add(shape);
        }

        BuildOverlay(r);

        switch (r.Verdict)
        {
            case Verdict.Pass:
                State = ScreenState.Pass;
                BannerText = "PASS";
                BannerNote = prevIds is null ? "" : $"수리 완료 · {prevIds.Count}건 모두 해결";
                StatusMessage = "양품 — 다음 보드를 올리세요";
                _lastSummary = $"{no} PASS";
                _prevDefects = null;
                _passTimer.Start();   // 잠시 보여준 뒤 대기 화면으로
                break;

            case Verdict.Fail:
                State = ScreenState.Fail;
                BannerText = $"FAIL · {r.DefectCount}건";
                BannerNote = BuildCompareNote(prevIds is not null, resolved, defects.Count, newIds.Count);
                StatusMessage = "";
                BuildDefectCards(defects, newIds);
                _lastSummary = $"{no} FAIL {r.DefectCount}건";
                _prevDefects = defects;
                break;

            default:
                // 판정 불가: 같은 보드의 직전 FAIL 기록은 그대로 둔다 (다음 재검사에서 비교할 수 있게)
                State = ScreenState.Invalid;
                BannerText = "판정 불가";
                StatusMessage = ToErrorMessage(r.ErrorCode);
                BoardParts.Clear();
                _lastSummary = $"{no} 판정 불가";
                break;
        }
    }

    /// <summary>"1건 해결 · 남은 불량 1건 · 새 불량 1건" + 재검사 횟수 안내</summary>
    private string BuildCompareNote(bool isRetest, int resolved, int remaining, int added)
    {
        var parts = new List<string>();
        if (isRetest)
        {
            if (resolved > 0) parts.Add($"{resolved}건 해결");
            parts.Add($"남은 불량 {remaining}건");
            if (added > 0) parts.Add($"새 불량 {added}건");
        }
        if (_attempt >= _maxAttempts)
            parts.Add($"{_attempt}회째 — 수리가 어려우면 불량 확정(F)");
        return string.Join(" · ", parts);
    }

    /// <summary>수리해도 안 되는 보드를 빼낸다. 화면은 즉시 대기로 돌아간다.</summary>
    private bool CanReject() => State == ScreenState.Fail;

    [RelayCommand(CanExecute = nameof(CanReject))]
    private void Reject()
    {
        _lastSummary = $"{InspectionStamp.Split(' ')[0]} 불량 확정";
        GoIdle();
        StatusMessage = "불량 처리 기록됨 — 다음 보드를 올리세요";
    }

    // ── 불량 카드 ───────────────────────────────────────────

    private void BuildDefectCards(List<PartResult> defects, HashSet<string> newIds)
    {
        // 카드 칸은 최대 3장 높이. 불량이 3건 이하면 전부 카드로,
        // 4건 이상이면 2장 + "외 N건" 요약 1장으로 3장을 넘지 않게 한다 (4장째가 잘려 보이던 문제).
        // 보드 위 번호 배지와 아래 목록에는 모든 불량이 그대로 나온다.
        int maxCards = defects.Count <= 3 ? 3 : 2;

        for (int i = 0; i < Math.Min(defects.Count, maxCards); i++)
        {
            var p = defects[i];
            string tag = newIds.Contains(p.PartId) ? "  (새로 생김)" : "";
            DefectCards.Add(new DefectCard(
                Badge: (i + 1).ToString(),
                Title: $"{p.PartId} {DescribeDefect(p.Defect)}{tag}",
                Detail: BuildDetail(p),
                Action: BuildAction(p)));
        }

        if (defects.Count > maxCards)
        {
            DefectCards.Add(new DefectCard(
                Badge: "+",
                Title: $"외 {defects.Count - maxCards}건",
                Detail: string.Join(", ", defects.Skip(maxCards)
                    .Select((p, k) => $"{maxCards + k + 1}번 {p.PartId} {DescribeDefect(p.Defect)}")),
                Action: "보드 위 번호와 아래 목록에서 확인하세요"));
        }
    }

    /// <summary>기대와 실제를 화살표로. 한눈에 비교하게.</summary>
    private static string BuildDetail(PartResult p) => p.Defect switch
    {
        DefectType.Missing => $"{Korean(p.ExpectedClass)} → 없음",
        DefectType.Extra => $"없어야 함 → {Korean(p.DetectedClass)}",
        DefectType.Offset => $"{Holes(p.ExpectedHoles)} 중 {Holes(Uncovered(p))} 비어 있음",
        _ => $"{Korean(p.ExpectedClass)} → {Korean(p.DetectedClass)}"
    };

    /// <summary>수리 공정이므로 "무엇을 어디에" 하라는 지시를 준다. 보드 위 동작 표시와 같은 동사를 쓴다.</summary>
    private static string BuildAction(PartResult p) => p.Defect switch
    {
        DefectType.Missing => $"꽂기: {Holes(p.ExpectedHoles)}에 {WithObject(Korean(p.ExpectedClass))} 꽂으세요",
        DefectType.WrongPart => $"교체: {WithDirection(Korean(p.ExpectedClass))} 바꾸세요",
        DefectType.WireColorSwap => $"교체: {WithDirection(Korean(p.ExpectedClass))} 바꾸세요",
        DefectType.Offset => $"옮기기: {Holes(p.ExpectedHoles)}에 맞춰 다시 꽂으세요",
        DefectType.Extra => $"빼기: {Holes(p.CoveredHoles)}의 부품을 빼세요",
        _ => "확인하세요"
    };

    /// <summary>
    /// 홀 목록을 짧게. 같은 열에서 행이 연속이면 "H11~M11", 아니면 "Q9,Q10".
    /// (긴 전선은 홀이 6개라 그대로 나열하면 카드 한 줄을 넘는다)
    /// </summary>
    private static string Holes(List<Hole> holes)
    {
        if (holes.Count == 0) return "-";
        if (holes.Count >= 3 && holes.Select(h => h.Col).Distinct().Count() == 1)
        {
            var rows = holes.Select(h => h.Row).OrderBy(r => r).ToList();
            // 전원 레일이 아니면 E·K·Q도 실제 홀이라 연속으로 본다
            bool consecutive = rows.Zip(rows.Skip(1), (a, b) => b - a).All(d => d == 1);
            if (consecutive) return $"{rows[0]}{holes[0].Col}~{rows[^1]}{holes[0].Col}";
        }
        if (holes.Count >= 3 && holes.Select(h => h.Row).Distinct().Count() == 1)
        {
            var cols = holes.Select(h => h.Col).OrderBy(c => c).ToList();
            bool consecutive = cols.Zip(cols.Skip(1), (a, b) => b - a).All(d => d == 1);
            if (consecutive) return $"{holes[0].Row}{cols[0]}~{holes[0].Row}{cols[^1]}";
        }
        return string.Join(",", holes);
    }

    /// <summary>레시피 홀 중 비어 있는 홀 (위치 불량에서 "어디가 빠졌나").</summary>
    private static List<Hole> Uncovered(PartResult p) =>
        p.ExpectedHoles.Where(h => !p.CoveredHoles.Contains(h)).ToList();

    // ── 한국어 조사 ─────────────────────────────────────────
    // "저항 470Ω을(를)" 대신 받침에 맞춰 "을/를", "으로/로"를 고른다.
    // 영문·기호로 끝나면 읽는 소리 기준으로 대략 판단한다 (Ω=옴 → 받침 있음, LED=엘이디 → 없음).

    private static bool HasFinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        char last = word[^1];
        if (last >= '가' && last <= '힣') return (last - '가') % 28 != 0;
        return last switch
        {
            'Ω' => true,                                  // 옴
            'L' or 'M' or 'N' or 'R' => true,             // 엘 엠 엔 알
            _ when char.IsDigit(last) => "0136780".Contains(last),   // 영 일 삼 육 칠 팔 공
            _ => false                                    // D(디), P(피) 등
        };
    }

    private static bool EndsWithRieul(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        char last = word[^1];
        if (last >= '가' && last <= '힣') return (last - '가') % 28 == 8;   // ㄹ 받침
        return last is 'L' or '1' or '7' or '8';                            // 엘 일 칠 팔
    }

    private static string WithObject(string word) => word + (HasFinalConsonant(word) ? "을" : "를");

    private static string WithDirection(string word) =>
        word + (HasFinalConsonant(word) && !EndsWithRieul(word) ? "으로" : "로");

    // ── 로그인 / 로그아웃 ────────────────────────────────────

    /// <summary>
    /// 사번 + PIN 로그인. PIN은 보안상 바인딩하지 않고 PasswordBox를 직접 받아 읽은 뒤 바로 지운다.
    /// 1) 스테이션을 거쳐 MES에 로그인 → 성공하면 이 PC에 오프라인용 해시 저장
    /// 2) MES나 스테이션에 연결할 수 없으면 저장된 해시로 오프라인 로그인
    /// </summary>
    [RelayCommand]
    private async Task LoginAsync(object? parameter)
    {
        if (IsLoggingIn) return;
        var box = parameter as System.Windows.Controls.PasswordBox;
        string id = LoginEmployeeId.Trim();
        string pin = box?.Password ?? "";

        if (id.Length == 0 || pin.Length == 0)
        {
            LoginMessage = "사번과 PIN을 입력하세요.";
            return;
        }

        IsLoggingIn = true;
        LoginMessage = "로그인 중...";
        try
        {
            var outcome = await _client.LoginAsync(id, pin);
            switch (outcome.Status)
            {
                case LoginStatus.Ok when outcome.Worker is not null:
                    if (!outcome.Worker.Offline) _session.Remember(id, pin, outcome.Worker);
                    SignIn(outcome.Worker);
                    ShowRecipeSync(outcome.RecipeSync);
                    break;

                case LoginStatus.InvalidCredentials:
                    LoginMessage = outcome.Message ?? "사번 또는 PIN이 올바르지 않습니다.";
                    break;

                default:
                    // MES(또는 스테이션)가 안 될 때: 전에 로그인한 적 있는 작업자면 저장된 정보로
                    var offline = _session.TryOffline(id, pin);
                    if (offline is not null)
                    {
                        SignIn(offline);
                        ShowRecipeSync(outcome.RecipeSync);   // 스테이션이 준 OFFLINE 목록 (스테이션에도 못 닿았으면 없음)
                    }
                    else LoginMessage = $"{outcome.Message} 이 PC에서 로그인한 적이 없는 작업자는 오프라인 로그인을 할 수 없습니다.";
                    break;
            }
        }
        finally
        {
            box?.Clear();
            IsLoggingIn = false;
        }
    }

    private void SignIn(LoginResult worker)
    {
        _session.SignIn(worker);
        CurrentWorker = worker;
        LoginEmployeeId = "";
        LoginMessage = "";
        GoIdle();
    }

    private bool CanLogout() => IsLoggedIn && State != ScreenState.Busy;

    [RelayCommand(CanExecute = nameof(CanLogout))]
    private void Logout()
    {
        _session.SignOut();
        CurrentWorker = null;
        LastResultNote = "";
        _lastSummary = "";
        GoIdle();
    }

    // ── 입력 (카메라 / 사진) ─────────────────────────────────
    private bool NotBusy() => State != ScreenState.Busy;

    /// <summary>사진 파일로 검사 (카메라가 안 될 때의 대체 수단).</summary>
    [RelayCommand(CanExecute = nameof(NotBusy))]
    private void OpenImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "검사할 사진 선택",
            Filter = "이미지 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|모든 파일 (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        _input.Camera.Stop();            // 파일 모드에서는 카메라를 놓아준다 (Camo 앱과 충돌 방지)
        _input.File.SetPath(dialog.FileName);
        _input.Mode = InputMode.File;
        RefreshInputInfo();
        LoadFilePreview();

        // FAIL·판정불가 중에 사진을 바꾸면 "고친 보드의 사진"으로 보고 재검사 흐름을 유지한다.
        // (카메라가 안 될 때 사진 두 장으로 수리→재검사를 시연할 수 있게)
        if (State is ScreenState.Fail or ScreenState.Invalid or ScreenState.Error)
        {
            ImageDescription = _input.Description + " — Space로 재검사";
            return;
        }
        CapturedImage = null;
        CapturedFull = null;
        GoIdle();
    }

    /// <summary>카메라 모드로 돌아간다.</summary>
    [RelayCommand(CanExecute = nameof(NotBusy))]
    private void UseCamera()
    {
        _input.Mode = InputMode.Camera;
        LiveImage = null;
        CapturedImage = null;
        CapturedFull = null;
        _input.Camera.Start();
        RefreshInputInfo();
        GoIdle();
    }

    /// <summary>
    /// 다음 번호의 카메라로. 0은 보통 노트북 내장, Camo는 1~2번인 경우가 많다.
    /// 화면에 Camo 영상이 나오는 번호를 찾으면 appsettings.json의 Camera:Index에 적어둔다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(NotBusy))]
    private void NextCamera()
    {
        int next = IsCameraMode ? (_input.Camera.Index + 1) % (_maxCameraIndex + 1) : _input.Camera.Index;
        _input.Mode = InputMode.Camera;
        LiveImage = null;
        CapturedImage = null;
        CapturedFull = null;
        CameraOk = null;
        _input.Camera.Start(next);
        RefreshInputInfo();
        GoIdle();
    }

    private void OnFrameArrived(BitmapSource frame)
    {
        // 백그라운드 스레드에서 온다. UI가 바쁘면 이번 프레임은 버린다 (밀리면 화면이 늦게 따라온다).
        if (Interlocked.Exchange(ref _previewPending, 1) == 1) return;
        var cropped = CropToGuide(frame);   // 자르기도 여기서 (UI 스레드 부담을 줄인다)
        _ui.BeginInvoke(DispatcherPriority.Render, () =>
        {
            if (IsCameraMode) LiveImage = cropped;
            Interlocked.Exchange(ref _previewPending, 0);
        });
    }

    private void OnCameraConnectionChanged(bool connected)
    {
        _ui.BeginInvoke(() =>
        {
            CameraOk = connected;
            if (!connected && IsCameraMode) LiveImage = null;
            RefreshInputInfo();
        });
    }

    private void RefreshInputInfo()
    {
        IsCameraMode = _input.Mode == InputMode.Camera;
        ImageDescription = _input.Description;
        if (!IsCameraMode) CameraOk = null;
        RaisePanelChanged();
    }

    private void LoadFilePreview()
    {
        try
        {
            LiveImage = _input.File.Path is { } p && File.Exists(p) ? Decode(File.ReadAllBytes(p)) : null;
        }
        catch
        {
            LiveImage = null;
        }
    }

    /// <summary>
    /// 화면에서 가이드 영역(+여백)만 잘라낸다. 화면 표시용일 뿐, 추론 서비스에는 원본 전체를 보낸다.
    /// 가이드는 검사 영역 비율(GuideAspect)을 유지하며, 가로·세로 중 먼저 닿는 쪽에 맞춰 GuideFill만큼 채운다.
    /// 예: 1280x720, 비율 0.97, 90% → 가이드 629x648, 여백 포함 잘라내기 692x713
    /// </summary>
    private BitmapSource CropToGuide(BitmapSource src)
    {
        double gh = Math.Min(src.PixelHeight * _guideFill, src.PixelWidth * _guideFill / _guideAspect);
        double gw = gh * _guideAspect;
        double w = Math.Min(src.PixelWidth, gw * (1 + CropMargin * 2));
        double h = Math.Min(src.PixelHeight, gh * (1 + CropMargin * 2));
        var rect = new Int32Rect(
            (int)((src.PixelWidth - w) / 2), (int)((src.PixelHeight - h) / 2), (int)w, (int)h);
        if (rect.Width <= 0 || rect.Height <= 0) return src;

        var cropped = new CroppedBitmap(src, rect);
        cropped.Freeze();
        return cropped;
    }

    /// <summary>JPEG 바이트 → 화면용 이미지. 폰 사진(4000px 등)은 화면용으로 줄여서 연다.</summary>
    private static BitmapSource Decode(byte[] bytes)
    {
        var bmp = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = 1600;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    // ── 기타 ────────────────────────────────────────────────

    [RelayCommand]
    private void SelectPart(string partId)
        => SelectedPart = Parts.FirstOrDefault(p => p.PartId == partId);

    /// <summary>모델 클래스 이름을 작업자가 읽는 말로. 화면에 모델 내부 이름을 노출하지 않는다.</summary>
    public static string Korean(string? cls) => cls switch
    {
        null or "-" => "없음",
        "resistance470" => "저항 470Ω",
        "resistance330" => "저항 330Ω",
        "ic_chip" => "IC 칩",
        "led" => "LED",
        "capacitor" => "캐패시터",
        "red_wire" => "빨간 전선",
        "black_wire" => "검은 전선",
        "white_wire" => "흰 전선",
        _ => cls
    };

    public static string DescribeDefect(DefectType? defect) => defect switch
    {
        null => "정상",
        DefectType.Missing => "누락",
        DefectType.WrongPart => "다른 부품",
        DefectType.WireColorSwap => "전선 색",
        DefectType.Offset => "위치",
        DefectType.Extra => "추가",
        _ => "불량"
    };

    private static string ToErrorMessage(string? code) => code switch
    {
        "BOARD_NOT_RECOGNIZED" => "보드를 노란 점선 안에 꽉 차게 맞추고 재검사하세요",
        "INFERENCE_TIMEOUT" => "처리 시간이 초과됐습니다 — 재검사하세요",
        "INFERENCE_UNAVAILABLE" => "추론 서비스 연결을 확인하세요 (관리자 호출)",
        "MARKER_NOT_FOUND" => "보드 위치를 확인하고 재검사하세요",
        "CAPTURE_QUALITY" => "조명과 초점을 확인하고 재검사하세요",
        _ => "재검사하세요"
    };
}
