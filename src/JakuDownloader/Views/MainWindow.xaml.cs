using JakuDownloader.Helpers;
using JakuDownloader.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace JakuDownloader.Views;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 640;
    private const int DefaultHeight = 240;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // AppWindow trabaja en pixeles fisicos, hay que escalar segun el DPI
        var scale = NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));
        AppWindow.SetIcon(AppPaths.IconIcoPath);

        Closed += (_, _) => ViewModel.CancelDownload();
    }

    private void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.DownloadCommand.CanExecute(null))
        {
            ViewModel.DownloadCommand.Execute(null);
            e.Handled = true;
        }
    }
}
