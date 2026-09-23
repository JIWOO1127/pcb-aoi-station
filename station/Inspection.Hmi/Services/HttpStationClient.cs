using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inspection.Contracts;

namespace Inspection.Hmi.Services;

/// <summary>
/// 스테이션 서버(ASP.NET Core)의 POST /api/trigger를 호출한다.
/// 주소는 appsettings.json의 Station:BaseUrl.
/// </summary>
public class HttpStationClient : IStationClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter());   // "Fail" 같은 글자를 enum으로
        return o;
    }

    public HttpStationClient(HttpClient http) => _http = http;

    public async Task<InspectionResult> TriggerAsync(byte[] imageBytes, string productId,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        var image = new ByteArrayContent(imageBytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(image, "image", "board.jpg");
        content.Add(new StringContent(productId), "productId");

        using var response = await _http.PostAsync("/api/trigger", content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<InspectionResult>(JsonOptions, ct);

        return result ?? throw new InvalidOperationException("스테이션 응답을 읽을 수 없습니다.");
    }
}