using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inspection.Contracts;

namespace Inspection.Hmi.Services;

/// <summary>
/// 서버 없이 화면을 개발하기 위한 가짜 클라이언트.
/// MockData/mock-results.json의 결과를 순서대로 돌려준다.
/// </summary>
public class MockStationClient : IStationClient
{
    private readonly List<InspectionResult> _samples;
    private int _index;

    public MockStationClient()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "MockData", "mock-results.json");
        var json = File.ReadAllText(path);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new HoleJsonConverter()); // "A0" <-> Hole

        _samples = JsonSerializer.Deserialize<List<InspectionResult>>(json, options)
                   ?? throw new InvalidOperationException("mock-results.json을 읽을 수 없습니다.");
    }

    public async Task<InspectionResult> TriggerAsync(string productId, CancellationToken ct = default)
    {
        await Task.Delay(800, ct);
        var sample = _samples[_index++ % _samples.Count];
        return sample with { InspectionId = Guid.NewGuid() };
    }
}

/// <summary>"A0" 같은 문자열을 Hole(Row='A', Col=0)로 바꿔주는 변환기.</summary>
public class HoleJsonConverter : JsonConverter<Hole>
{
    public override Hole Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        var s = reader.GetString()!;   // 예: "A0", "U13"
        char row = s[0];
        int col = int.Parse(s[1..]);
        return new Hole(row, col);
    }

    public override void Write(Utf8JsonWriter writer, Hole value, JsonSerializerOptions o)
        => writer.WriteStringValue(value.ToString());
}