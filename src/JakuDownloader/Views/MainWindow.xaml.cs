using JakuDownloader.Helpers;
using JakuDownloader.Services;
using JakuDownloader.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace JakuDownloader.Views;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1040;
    private const int DefaultHeight = 780;
    private const int MinimumWidth = 640;
    private const int MinimumHeight = 500;

    private readonly DownloadQueueViewModel _queue;
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly TrayIcon? _tray;
    private CloseState _closeState = CloseState.Open;
    private double _appliedScale;
    private bool _hasBeenShown;
    private bool _isHidden;

    public MainWindow(DownloadQueueViewModel queue, SettingsService settings, NotificationService notifications)
    {
        _queue = queue;
        _settings = settings;
        _notifications = notifications;
        InitializeComponent();

        // Los avisos de Windows de inicio y error solo se muestran si la ventana no esta al frente
        Activated += (_, args) => App.IsMainWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon(AppPaths.IconIcoPath);

        // AppWindow trabaja en pixeles fisicos, hay que escalar segun el DPI
        var scale = NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));
        ApplyMinimumSize(scale);

        // Al pasar a un monitor con otro DPI el minimo en pixeles cambia
        RootGrid.Loaded += (_, _) => RootGrid.XamlRoot.Changed += (root, _) => ApplyMinimumSize(root.RasterizationScale);

        NavView.SelectedItem = NavView.MenuItems[0];

        try
        {
            _tray = new TrayIcon(AppPaths.IconIcoPath, BuildTrayMenu);
            _tray.OpenRequested += (_, _) => BringToFront();
            _queue.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DownloadQueueViewModel.Summary)) UpdateTrayToolTip();
            };
            _settings.Changed += (_, _) => UpdateTrayVisibility();
            UpdateTrayToolTip();
            UpdateTrayVisibility();
        }
        catch (InvalidOperationException)
        {
            // Sin icono no hay segundo plano: cerrar la ventana vuelve a cerrar la app
        }

        AppWindow.Closing += AppWindow_Closing;
        Closed += (_, _) =>
        {
            _tray?.Dispose();
            _queue.CancelAllCommand.Execute(null);
        };
    }

    private enum CloseState
    {
        Open,
        Confirming,
        Finishing,
        Ready,
    }

    private bool CanHide => _tray is not null && _settings.KeepRunningInBackground;

    /// <summary>
    /// Arranque desde el inicio de Windows: la ventana no se muestra hasta que la pidan desde el icono.
    /// </summary>
    public void StartInBackground()
    {
        // Sin icono no habria forma de abrirla
        if (_tray is null)
        {
            BringToFront();
            return;
        }

        _isHidden = true;
        App.IsMainWindowActive = false;
        UpdateTrayVisibility();
    }

    /// <summary>
    /// Muestra la ventana y la trae al frente, este oculta, minimizada o detras de otras.
    /// </summary>
    public void BringToFront()
    {
        _isHidden = false;
        UpdateTrayVisibility();

        // La primera vez Activate crea el contenido; despues hay que volver a mostrar la ventana oculta
        if (_hasBeenShown) AppWindow.Show();
        _hasBeenShown = true;
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter) presenter.Restore();
        Activate();
        NativeMethods.SetForegroundWindow(App.MainWindowHandle);
    }

    private void HideToBackground()
    {
        _isHidden = true;
        AppWindow.Hide();
        App.IsMainWindowActive = false;
        UpdateTrayVisibility();

        // Solo la primera vez: si no, parece que la app se cerro con las descargas o que se colgo
        if (_settings.Current.BackgroundNoticeShown) return;

        _notifications.Show("Jaku Downloader sigue abierto",
            "Las descargas continúan. Para volver, usá el ícono junto al reloj; para salir, elegí Salir en su menú.");
        try
        {
            _settings.Update(current => current with { BackgroundNoticeShown = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Se vuelve a avisar la proxima vez, nada mas
        }
    }

    private void UpdateTrayVisibility()
    {
        // Con la ventana oculta el icono es la unica forma de volver, aunque se haya desactivado el segundo plano
        if (_tray is not null) _tray.IsVisible = _isHidden || _settings.KeepRunningInBackground;
    }

    private void UpdateTrayToolTip()
    {
        if (_tray is not null) _tray.ToolTip = _queue.HasItems ? $"Jaku Downloader\n{_queue.Summary}" : "Jaku Downloader";
    }

    private IReadOnlyList<TrayMenuItem> BuildTrayMenu()
    {
        // Una sola opcion que pausa o reanuda segun haya algo descargando
        var pauseOrResume = _queue.PauseAllCommand.CanExecute(null)
            ? new TrayMenuItem("Pausar todo", () => _queue.PauseAllCommand.Execute(null))
            : new TrayMenuItem("Reanudar todo", () => _queue.ResumeAllCommand.Execute(null), _queue.ResumeAllCommand.CanExecute(null));

        return
        [
            new TrayMenuItem("Abrir Jaku Downloader", BringToFront, IsDefault: true),
            TrayMenuItem.Separator,
            pauseOrResume,
            new TrayMenuItem("Abrir la carpeta de descargas", OpenDownloadFolder),
            TrayMenuItem.Separator,
            new TrayMenuItem("Salir", () => _ = ExitAsync()),
        ];
    }

    private void OpenDownloadFolder()
    {
        if (!ShellHelper.TryOpenFolder(_settings.DownloadDirectory, out var error))
        {
            _notifications.Show("No se pudo abrir la carpeta", error);
        }
    }

    private void ApplyMinimumSize(double scale)
    {
        if (scale == _appliedScale || AppWindow.Presenter is not OverlappedPresenter presenter) return;

        _appliedScale = scale;
        presenter.PreferredMinimumWidth = (int)(MinimumWidth * scale);
        presenter.PreferredMinimumHeight = (int)(MinimumHeight * scale);
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeState == CloseState.Ready) return;

        // Con el segundo plano activo, la X solo oculta la ventana y las descargas siguen
        if (_closeState == CloseState.Open && CanHide)
        {
            args.Cancel = true;
            HideToBackground();
            return;
        }

        if (_closeState == CloseState.Open && _queue.PendingCount == 0) return;

        args.Cancel = true;
        await ExitAsync();
    }

    /// <summary>
    /// Cierra la app. Si hay descargas sin terminar, pregunta antes y espera a que se cancelen y limpien.
    /// </summary>
    private async Task ExitAsync()
    {
        // Mientras se pregunta o se termina de cancelar, los otros intentos de salir se ignoran
        if (_closeState != CloseState.Open) return;

        if (_queue.PendingCount > 0)
        {
            _closeState = CloseState.Confirming;

            // Desde el icono la ventana puede estar oculta, y la pregunta tiene que verse
            BringToFront();

            if (!await ConfirmExitAsync(_queue.PendingCount))
            {
                _closeState = CloseState.Open;
                return;
            }

            _closeState = CloseState.Finishing;
            await _queue.CancelAllAndWaitAsync();
        }

        _closeState = CloseState.Ready;
        Close();
    }

    private async Task<bool> ConfirmExitAsync(int count)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = count == 1 ? "Hay una descarga sin terminar" : $"Hay {count} descargas sin terminar",
            Content = "Si cerrás ahora, se cancelan y se borran los archivos parciales, incluidas las que están en pausa.",
            PrimaryButtonText = "Cancelar y salir",
            CloseButtonText = "Seguir descargando",
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception)
        {
            // Solo puede haber un dialogo abierto a la vez (por ejemplo "Elegir videos"): ante la duda no se cierra nada
            return false;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var pageType = args.IsSettingsSelected
            ? typeof(SettingsPage)
            : (args.SelectedItemContainer?.Tag as string) switch
            {
                "queue" => typeof(QueuePage),
                "history" => typeof(HistoryPage),
                _ => typeof(DownloadPage),
            };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, null, args.RecommendedNavigationTransitionInfo);
        }
    }
}
