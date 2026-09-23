using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Inspection.Station.Services;

/// <summary>
/// 추론 서비스(Python, /infer)가 돌려주는 핀홀 하나의 결과.
/// 아직 판정 전 상태 — "어느 핀홀에 어떤 부품이 있는지"만 담는다.
/// (Inspection.Contracts의 PartResult와는 다른 것. 그건 스테이션이 레시피와
/// 비교해서 판정까지 낸 뒤의 결과다.)
/// </summary>
public record InferPart(string Pinhole, string Class, double Confidence);

/// <summary>추론 서비스 /infer 응답 전체. docs/api-contract.md 참고.</summary>
public record InferResponse(List<InferPart> Parts);

public interface IInferenceClient
{
    Task<InferResponse> InferAsync(byte[] jpegBytes, CancellationToken ct = default);
}

/// <summary>
/// 추론 서비스(Python FastAPI, /infer)를 호출하는 HTTP 클라이언트.
/// 베이스 주소는 appsettings.json의 InferenceService:BaseUrl.
/// </summary>
public class InferenceClient : IInferenceClient
{
    private readonly HttpClient _http;

    public InferenceClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<InferResponse> InferAsync(byte[] jpegBytes, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(jpegBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "image", "image.jpg");

        using var response = await _http.PostAsync("/infer", content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<InferResponse>(cancellationToken: ct);
        return result ?? new InferResponse(new List<InferPart>());
    }
}
