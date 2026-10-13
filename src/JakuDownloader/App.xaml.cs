using JakuDownloader.Helpers;
using JakuDownloader.Services;
using JakuDownloader.ViewModels;
using JakuDownloader.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace JakuDownloader;

public partial class App : Application
{
    private readonly IHost _host;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ToolLocator>();
                services.AddSingleton<DownloadService>();
                services.AddSingleton<NotificationService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    public static T GetService<T>() where T : notnull
        => ((App)Current)._host.Services.GetRequiredService<T>();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Tiene que ir antes de crear la ventana
        AppIdentity.Apply();
        GetService<NotificationService>().Initialize();

        _window = GetService<MainWindow>();
        _window.Closed += (_, _) => _host.Dispose();
        _window.Activate();
    }
}
