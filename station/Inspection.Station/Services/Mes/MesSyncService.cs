using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Inspection.Station.Services.Mes;

/// <summary>
/// 보관함(MesOutbox)에 쌓인 기록을 MES로 보내는 백그라운드 작업.
/// 스테이션이 켜져 있는 동안 계속 돈다.
///
/// 응답별 처리:
///   2xx          저장됨 → 파일 삭제
///   409 Conflict 이미 저장된 기록(재전송) → 성공으로 보고 파일 삭제
///   그 밖의 4xx  기록 자체가 잘못됨 → 계속 보내도 소용없으므로 failed 폴더로 옮기고 다음 것으로
///   5xx, 연결 실패, 시간 초과 → MES 문제 → 이번 회차는 멈추고 RetrySeconds 뒤에 다시
///   (한 건이 실패하면 뒤의 것도 실패할 가능성이 높아서, 계속 두드리지 않는다)
/// </summary>
public class MesSyncService : BackgroundService
{
    private readonly MesOutbox _outbox;
    private readonly HttpClient _http;
    private readonly MesOptions _options;
    private readonly ILogger<MesSyncService> _log;

    public MesSyncService(MesOutbox outbox, IHttpClientFactory httpFactory,
        IOptions<MesOptions> options, ILogger<MesSyncService> log)
    {
        _outbox = outbox;
        _http = httpFactory.CreateClient("Mes");
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _log.LogInformation("MES 전송 꺼짐 (Mes:Enabled=false)");
            return;
        }

        _log.LogInformation("MES 전송 시작: {Url}{Path}, 보관함 {Folder}",
            _options.BaseUrl, _options.InspectionsPath, _outbox.Folder);

        var retry = TimeSpan.FromSeconds(Math.Max(1, _options.RetrySeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await SendPendingAsync(stoppingToken);

            try
            {
                await _outbox.WaitAsync(retry, stoppingToken);   // 새 기록이 오면 바로, 아니면 retry 뒤에
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendPendingAsync(CancellationToken ct)
    {
        foreach (var file in _outbox.PendingFiles())
        {
            if (ct.IsCancellationRequested) return;

            string json;
            try
            {
                json = await File.ReadAllTextAsync(file, ct);
            }
            catch (IOException)
            {
                continue;   // 아직 쓰는 중이면 다음 회차에
            }

            try
            {
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(_options.InspectionsPath, content, ct);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
                {
                    File.Delete(file);
                    _outbox.ReportSuccess();
                    continue;
                }

                int code = (int)response.StatusCode;
                if (code is >= 400 and < 500)
                {
                    // 보내는 내용이 규격과 안 맞음. 사람이 봐야 하므로 따로 빼 두고 다음 기록으로.
                    string body = await response.Content.ReadAsStringAsync(ct);
                    MoveToFailed(file);
                    _outbox.ReportFailure($"MES가 기록을 거절함 ({code}) — failed 폴더 확인");
                    _log.LogWarning("MES 거절 {Code}: {File} {Body}", code, Path.GetFileName(file), body);
                    continue;
                }

                _outbox.ReportFailure($"MES 서버 오류 ({code})");
                _log.LogWarning("MES 서버 오류 {Code}, {Sec}초 뒤 재시도", code, _options.RetrySeconds);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                // MES가 꺼져 있거나, 주소가 틀리거나, 네트워크가 끊김
                _outbox.ReportFailure("MES에 연결할 수 없음");
                _log.LogDebug("MES 연결 실패: {Message}", ex.Message);
                return;
            }
        }
    }

    private void MoveToFailed(string file)
    {
        string failed = Path.Combine(_outbox.Folder, "failed");
        Directory.CreateDirectory(failed);
        File.Move(file, Path.Combine(failed, Path.GetFileName(file)), overwrite: true);
    }
}
