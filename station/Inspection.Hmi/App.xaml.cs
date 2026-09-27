using System.IO;
using System.Windows;
using Inspection.Hmi.Services;
using Inspection.Hmi.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Inspection.Hmi;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(c =>
            {
                c.SetBasePath(AppContext.BaseDirectory);
                c.AddJsonFile("appsettings.json", optional: false);
            })
            .ConfigureServices((context, services) =>
            {
                // 검사할 이미지 공급처: 카메라(기본) ↔ 사진 파일 전환.
                // 카메라는 앱이 끝날 때 DI가 Dispose해서 장치를 놓아준다.
                services.AddSingleton<CameraImageSource>();
                services.AddSingleton<FileImageSource>();
                services.AddSingleton<SwitchableImageSource>();
                services.AddSingleton<IImageSource>(sp => sp.GetRequiredService<SwitchableImageSource>());

                // 스테이션 서버 호출.
                // 서버 없이 화면만 보려면 아래 두 줄을 주석 처리하고
                // services.AddSingleton<IStationClient, MockStationClient>(); 를 쓴다.
                services.AddHttpClient<IStationClient, HttpStationClient>(client =>
                {
                    var baseUrl = context.Configuration["Station:BaseUrl"]
                                  ?? "http://localhost:5215";
                    client.BaseAddress = new Uri(baseUrl);
                    client.Timeout = TimeSpan.FromSeconds(30);
                });

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        _host.Services.GetRequiredService<MainWindow>().Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
