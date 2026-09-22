using Inspection.Station.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 추론 서비스(Python FastAPI, /infer) 호출용 클라이언트.
builder.Services.AddHttpClient<IInferenceClient, InferenceClient>(client =>
{
    var baseUrl = builder.Configuration["InferenceService:BaseUrl"] ?? "http://localhost:8001";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
