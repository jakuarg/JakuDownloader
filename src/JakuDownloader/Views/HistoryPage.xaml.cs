using JakuDownloader.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JakuDownloader.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryPage()
    {
        InitializeComponent();
    }

    public HistoryViewModel ViewModel { get; } = App.GetService<HistoryViewModel>();

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "¿Borrar el historial?",
            Content = "Se borra la lista de descargas terminadas. Los archivos descargados no se tocan.",
            PrimaryButtonText = "Borrar historial",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.Clear();
    }
}
