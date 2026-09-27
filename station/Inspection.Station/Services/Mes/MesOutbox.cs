using System.Text.Json;
using Inspection.Contracts;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Services.Mes;

/// <summary>
/// MES로 보낼 검사 기록을 스테이션 PC에 먼저 저장해 두는 보관함 ("쌓아두고 보내기").
///
/// 원칙: MES가 꺼져 있어도 검사는 멈추지 않는다.
///   1. 검사가 끝나면 기록을 파일로 저장한다 (여기, 즉시)
///   2. MesSyncService가 오래된 것부터 MES로 보내고, 성공하면 지운다
///   3. MES가 안 되면 파일이 그대로 남고, 다시 연결되면 밀린 것부터 보낸다
///
/// SQLite 대신 파일(JSON 한 건 = 파일 하나)을 쓰는 이유:
///   - SQLite는 네이티브 DLL이 필요한데, 이 PC의 보안 정책이 새 DLL을 막은 적이 있다 (torch, HMI)
///   - 폴더를 열면 밀린 기록이 눈에 보여서 발표·디버깅 때 설명하기 쉽다
///   - 이 규모(하루 수백 건)에서는 파일로 충분하다
/// </summary>
public class MesOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly MesOptions _options;
    private readonly ILogger<MesOutbox> _log;
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _statusLock = new();

    private bool? _connected;
    private DateTimeOffset? _lastSuccessAt;
    private string? _lastError;

    public MesOutbox(IOptions<MesOptions> options, ILogger<MesOutbox> log)
    {
        _options = options.Value;
        _log = log;
        Folder = Path.Combine(AppContext.BaseDirectory, _options.OutboxFolder);
        Directory.CreateDirectory(Folder);
    }

    public string Folder { get; }
    public bool Enabled => _options.Enabled;

    /// <summary>검사 결과를 보관함에 넣는다. 파일 쓰기만 하므로 검사 응답을 늦추지 않는다.</summary>
    public void Enqueue(InspectionResult result, string productId, string? workerId = null)
    {
        if (!_options.Enabled) return;

        var record = MesInspectionRecord.From(result, _options.StationId, productId, workerId,
            DateTimeOffset.Now);

        // 이름 앞에 시각을 붙여 "오래된 것부터" 정렬되게 한다
        string name = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{result.InspectionId}.json";
        string path = Path.Combine(Folder, name);

        try
        {
            // 임시 파일에 다 쓴 뒤 이름을 바꾼다 → 쓰는 도중 꺼져도 반쪽짜리 파일이 전송되지 않는다
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(record, Json));
            File.Move(temp, path);
            _signal.Release();   // 전송 쪽을 바로 깨운다 (다음 재시도 주기를 기다리지 않게)
        }
        catch (Exception ex)
        {
            // 저장 실패가 검사를 막으면 안 된다. 기록만 남긴다.
            _log.LogError(ex, "MES 보관함 저장 실패: {Id}", result.InspectionId);
        }
    }

    /// <summary>보낼 파일 목록 (오래된 것부터).</summary>
    public IReadOnlyList<string> PendingFiles() =>
        Directory.GetFiles(Folder, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList();

    public int PendingCount => Directory.GetFiles(Folder, "*.json").Length;

    /// <summary>새 기록이 들어오거나 제한 시간이 지날 때까지 기다린다.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);

    public void ReportSuccess()
    {
        lock (_statusLock)
        {
            _connected = true;
            _lastSuccessAt = DateTimeOffset.Now;
            _lastError = null;
        }
    }

    public void ReportFailure(string reason)
    {
        lock (_statusLock)
        {
            _connected = false;
            _lastError = reason;
        }
    }

    public MesStatus GetStatus()
    {
        lock (_statusLock)
        {
            return new MesStatus(_options.Enabled, _connected, PendingCount, _lastSuccessAt, _lastError);
        }
    }
}
