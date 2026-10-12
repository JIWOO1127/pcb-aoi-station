using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Inspection.Contracts;

namespace Inspection.Station.Services;

/// <summary>
/// 추론 서비스(Python, /infer)가 돌려주는 핀홀 하나의 결과.
/// 아직 판정 전 상태 — "어느 핀홀에 어떤 부품이 있는지"만 담는다.
/// (Inspection.Contracts의 PartResult와는 다른 것. 그건 스테이션이 레시피와
/// 비교해서 판정까지 낸 뒤의 결과다.)
/// </summary>
public record InferPart(string Pinhole, string Class, double Confidence);

/// <summary>
/// 추론 서비스 /infer 응답 전체. docs/api-contract.md 참고.
/// Overlay는 /infer?overlay=true일 때만 온다 (정렬된 사진·구멍 좌표·YOLO 박스). 없으면 null.
/// </summary>
public record InferResponse(List<InferPart> Parts,
    [property: JsonPropertyName("overlay")] InspectionOverlay? Overlay = null);

public interface IInferenceClient
{
    Task<InferResponse> InferAsync(byte[] jpegBytes, CancellationToken ct = default);
}

/// <summary>
/// 추론 서비스(Python FastAPI, /infer)를 호출하는 HTTP 클라이언트.
/// 베이스 주소는 appsettings.json의 InferenceService:BaseUrl.
/// InferenceService:RequestOverlay(기본 true)면 /infer?overlay=true로 요청한다.
/// overlay를 모르는 추론 서비스는 이 파라미터를 무시하고 기존 응답을 준다 → Overlay null.
/// </summary>
public class InferenceClient : IInferenceClient
{
    private readonly HttpClient _http;
    private readonly string _inferPath;

    public InferenceClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _inferPath = config.GetValue("InferenceService:RequestOverlay", true)
            ? "/infer?overlay=true"
            : "/infer";
    }

    public async Task<InferResponse> InferAsync(byte[] jpegBytes, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(jpegBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "image", "image.jpg");

        using var response = await _http.PostAsync(_inferPath, content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<InferResponse>(cancellationToken: ct);
        return result ?? new InferResponse(new List<InferPart>());
    }
}
