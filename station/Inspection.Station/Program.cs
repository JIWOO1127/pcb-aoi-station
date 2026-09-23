using Inspection.Station.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 추론 서비스(Python FastAPI, /infer) 호출용 클라이언트.
builder.Services.AddHttpClient<IInferenceClient, InferenceClient>(client =>
{
    var baseUrl = builder.Configuration["InferenceService:BaseUrl"] ?? "http://localhost:8001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// 레시피(정답지) 공급. 지금은 JSON 파일, MES 준비되면 MesRecipeProvider로 교체.
builder.Services.AddSingleton<IRecipeProvider, FileRecipeProvider>();

// 검사 흐름 지휘 (사진 → 추론 → 판정)
builder.Services.AddScoped<InspectionService>();

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