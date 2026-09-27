using Inspection.Station.Services;
using Inspection.Station.Services.Mes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 추론 서비스(Python FastAPI, /infer) 호출용 클라이언트.
builder.Services.AddHttpClient<IInferenceClient, InferenceClient>(client =>
{
    var baseUrl = builder.Configuration["InferenceService:BaseUrl"] ?? "http://localhost:8001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(20);
});

// 레시피(정답지) 공급. 지금은 JSON 파일, MES 준비되면 MesRecipeProvider로 교체.
builder.Services.AddSingleton<IRecipeProvider, FileRecipeProvider>();

// 검사 흐름 지휘 (사진 → 추론 → 판정)
builder.Services.AddScoped<InspectionService>();

// MES 전송: 검사 기록을 먼저 파일로 쌓고, 백그라운드에서 MES로 보낸다.
builder.Services.Configure<MesOptions>(builder.Configuration.GetSection("Mes"));
builder.Services.AddSingleton<MesOutbox>();
builder.Services.AddHostedService<MesSyncService>();
builder.Services.AddHttpClient("Mes", (sp, client) =>
{
    var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MesOptions>>().Value;
    client.BaseAddress = new Uri(o.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("X-Station-Key", o.StationKey);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 개발 중에는 HTTPS 리디렉션을 끈다 (HMI가 http로 호출하므로)
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

app.Run();