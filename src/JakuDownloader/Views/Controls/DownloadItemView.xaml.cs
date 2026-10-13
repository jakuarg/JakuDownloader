using JakuDownloader.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JakuDownloader.Views.Controls;

/// <summary>
/// Tarjeta de una descarga de la cola. La usan la pagina Descargar y la Cola.
/// </summary>
public sealed partial class DownloadItemView : UserControl
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(DownloadItemViewModel), typeof(DownloadItemView), new PropertyMetadata(null));

    public DownloadItemView()
    {
        InitializeComponent();
    }

    public DownloadItemViewModel? Item
    {
        get => (DownloadItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }
}
