using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inspection.Contracts;

namespace Inspection.Hmi.Services;

/// <summary>
/// 스테이션 서버(ASP.NET Core)를 호출한다.
///   POST /api/trigger  검사 (사진, 제품, 로그인한 작업자 사번)
///   POST /api/login    사번 + PIN 로그인 (스테이션이 MES에 물어본다)
/// 주소는 appsettings.json의 Station:BaseUrl.
/// </summary>
public class HttpStationClient : IStationClient
{
    private readonly HttpClient _http;
    private readonly WorkerSession _session;

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter());   // "Fail" 같은 글자를 enum으로
        return o;
    }

    public HttpStationClient(HttpClient http, WorkerSession session)
    {
        _http = http;
        _session = session;
    }

    public async Task<InspectionResult> TriggerAsync(byte[] imageBytes, string productId,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        var image = new ByteArrayContent(imageBytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(image, "image", "board.jpg");
        content.Add(new StringContent(productId), "productId");

        // 로그인한 작업자가 있으면 사번을 같이 보낸다 → 스테이션이 MES 기록에 넣는다
        if (_session.Current is { } worker)
            content.Add(new StringContent(worker.EmployeeId), "workerId");

        using var response = await _http.PostAsync("/api/trigger", content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<InspectionResult>(JsonOptions, ct);

        return result ?? throw new InvalidOperationException("스테이션 응답을 읽을 수 없습니다.");
    }

    public async Task<LoginOutcome> LoginAsync(string employeeId, string pin, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("/api/login",
                new LoginRequest(employeeId, pin), JsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                var worker = await response.Content.ReadFromJsonAsync<LoginResult>(JsonOptions, ct);
                return worker is null
                    ? new LoginOutcome(LoginStatus.Unavailable, null, "로그인 응답을 읽을 수 없습니다.")
                    : new LoginOutcome(LoginStatus.Ok, worker);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                return new LoginOutcome(LoginStatus.InvalidCredentials, null, "사번 또는 PIN이 올바르지 않습니다.");

            return new LoginOutcome(LoginStatus.Unavailable, null, "MES에 연결할 수 없습니다.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // 스테이션 자체에 닿지 못함
            return new LoginOutcome(LoginStatus.Unavailable, null, "스테이션에 연결할 수 없습니다.");
        }
    }
}
