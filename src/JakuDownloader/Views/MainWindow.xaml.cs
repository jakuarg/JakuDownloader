using JakuDownloader.Helpers;
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

    private readonly DownloadViewModel _downloadViewModel;
    private CloseState _closeState = CloseState.Open;
    private double _appliedScale;

    public MainWindow(DownloadViewModel downloadViewModel)
    {
        _downloadViewModel = downloadViewModel;
        InitializeComponent();

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

        AppWindow.Closing += AppWindow_Closing;
        Closed += (_, _) => _downloadViewModel.CancelDownload();
    }

    private enum CloseState
    {
        Open,
        Confirming,
        Finishing,
        Ready,
    }

    private void ApplyMinimumSize(double scale)
    {
        if (scale == _appliedScale || AppWindow.Presenter is not OverlappedPresenter presenter) return;

        _appliedScale = scale;
        presenter.PreferredMinimumWidth = (int)(MinimumWidth * scale);
        presenter.PreferredMinimumHeight = (int)(MinimumHeight * scale);
    }

    /// <summary>
    /// Si hay una descarga en curso, pregunta antes de cerrar y espera a que se cancele y limpie.
    /// </summary>
    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeState == CloseState.Ready) return;
        if (_closeState == CloseState.Open && !_downloadViewModel.IsDownloading) return;

        // Mientras se pregunta o se termina de cancelar, los otros intentos de cierre se ignoran
        args.Cancel = true;
        if (_closeState != CloseState.Open) return;

        _closeState = CloseState.Confirming;
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Hay una descarga en curso",
            Content = "Si cerrás ahora, la descarga se cancela y se borran los archivos parciales.",
            PrimaryButtonText = "Cancelar y salir",
            CloseButtonText = "Seguir descargando",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _closeState = CloseState.Open;
            return;
        }

        _closeState = CloseState.Finishing;
        await _downloadViewModel.CancelAndWaitAsync();
        _closeState = CloseState.Ready;
        Close();
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
