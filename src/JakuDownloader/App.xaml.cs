using JakuDownloader.Helpers;
using JakuDownloader.Services;
using JakuDownloader.ViewModels;
using JakuDownloader.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
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
                services.AddSingleton<YtDlpRunner>();
                services.AddSingleton<VideoInfoService>();
                services.AddSingleton<DownloadService>();
                services.AddSingleton<NotificationService>();
                services.AddSingleton<DownloadViewModel>();
                services.AddTransient<SettingsViewModel>();
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

        // Se queda antes de la ventana a proposito: tarda poco (~30 ms con 30 restos) y asi
        // no puede pisarse con una descarga nueva
        DownloadService.CleanupStaleTempFiles(AppPaths.DownloadDirectory);

        _window = GetService<MainWindow>();
        _window.Closed += (_, _) => _host.Dispose();
        _window.Activate();

        // Las notificaciones (~100 ms) recien hacen falta al iniciar una descarga
        _window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => GetService<NotificationService>().Initialize());
    }
}
