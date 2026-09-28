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
                var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct);
                return body is null
                    ? new LoginOutcome(LoginStatus.Unavailable, null, "로그인 응답을 읽을 수 없습니다.")
                    : new LoginOutcome(LoginStatus.Ok,
                        new LoginResult(body.EmployeeId, body.Name, body.Role, body.Offline),
                        RecipeSync: body.RecipeSync ?? []);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                return new LoginOutcome(LoginStatus.InvalidCredentials, null, "사번 또는 PIN이 올바르지 않습니다.");

            // 503: 스테이션은 살아 있고 MES만 안 됨 → 지금 쓰는 레시피 목록(OFFLINE)을 같이 준다
            return new LoginOutcome(LoginStatus.Unavailable, null, "MES에 연결할 수 없습니다.",
                await ReadRecipeSyncAsync(response, ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // 스테이션 자체에 닿지 못함
            return new LoginOutcome(LoginStatus.Unavailable, null, "스테이션에 연결할 수 없습니다.");
        }
    }

    // /api/login 200 응답 (스테이션 LoginResponse). recipeSync가 없는 옛 스테이션이면 null
    private record LoginResponse(string EmployeeId, string Name, string Role, bool Offline,
        List<RecipeSyncItem>? RecipeSync);

    private record UnavailableResponse(List<RecipeSyncItem>? RecipeSync);

    private static async Task<IReadOnlyList<RecipeSyncItem>> ReadRecipeSyncAsync(HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<UnavailableResponse>(JsonOptions, ct);
            return body?.RecipeSync ?? [];
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return [];   // 본문이 없거나 JSON이 아님 → 배너 없이 로그인만
        }
    }
    
    /// <summary>
    /// GET /api/mes/status. 5초마다 불린다.
    /// 실패하면 null("모름") → 헤더 점이 회색. 예외를 던지지 않아서 스테이션이 꺼져 있어도 HMI는 멀쩡하다.
    /// </summary>
    public async Task<MesStatus?> GetMesStatusAsync(CancellationToken ct = default)
    {
        // 상태 확인은 2초 안에 답이 없으면 포기한다 (검사용 긴 타임아웃을 기다리지 않도록)
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            return await _http.GetFromJsonAsync<MesStatus>("/api/mes/status", JsonOptions, timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
