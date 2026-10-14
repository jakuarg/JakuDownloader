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
                services.AddSingleton<SettingsService>();
                services.AddSingleton<ToolLocator>();
                services.AddSingleton<ToolInstaller>();
                services.AddSingleton<YtDlpRunner>();
                services.AddSingleton<VideoInfoService>();
                services.AddSingleton<DownloadService>();
                services.AddSingleton<NotificationService>();
                services.AddSingleton<PickerService>();
                services.AddSingleton<HistoryService>();
                services.AddSingleton<DownloadQueueViewModel>();
                services.AddSingleton<DownloadViewModel>();
                services.AddSingleton<HistoryViewModel>();
                services.AddSingleton<ToolsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    /// <summary>
    /// Ventana principal, para los dialogos de Windows que necesitan saber sobre que ventana abrirse.
    /// </summary>
    public static nint MainWindowHandle { get; private set; }

    /// <summary>
    /// True mientras la ventana esta al frente: ahi los avisos de Windows sobran porque se ve todo en la app.
    /// </summary>
    public static bool IsMainWindowActive { get; set; } = true;

    public static T GetService<T>() where T : notnull
        => ((App)Current)._host.Services.GetRequiredService<T>();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Al iniciar sesion Windows abre la app con este argumento: arranca oculta, solo con el icono junto al reloj
        var startInBackground = Environment.GetCommandLineArgs().Skip(1)
            .Contains(StartupRegistration.BackgroundArgument, StringComparer.OrdinalIgnoreCase);

        // Si ya esta abierta, se muestra esa ventana y esta instancia se cierra sin tocar nada.
        // Desde el inicio de Windows no se muestra: el usuario no la pidio
        if (!SingleInstance.TryClaim(openExisting: !startInBackground))
        {
            Exit();
            return;
        }

        // Tiene que ir antes de crear la ventana
        AppIdentity.Apply();

        // Se queda antes de la ventana a proposito: tarda poco (~30 ms con 30 restos) y asi
        // no puede pisarse con una descarga nueva
        DownloadService.CleanupStaleTempFiles(GetService<SettingsService>().DownloadDirectory);
        StartupRegistration.TryRefreshPath();

        _window = GetService<MainWindow>();
        MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _window.Closed += (_, _) => _host.Dispose();
        if (startInBackground) _window.StartInBackground();
        else _window.BringToFront();

        // Las notificaciones (~100 ms) recien hacen falta al iniciar una descarga
        _window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => GetService<NotificationService>().Initialize());
    }
}
