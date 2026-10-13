using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace JakuDownloader.Views;

/// <summary>
/// Funciones para usar en x:Bind cuando hace falta convertir un valor del ViewModel.
/// </summary>
public static class BindingFunctions
{
    // La miniatura se muestra a 192 px de ancho; se decodifica al doble para pantallas con escala alta
    private const int ThumbnailDecodeWidth = 384;

    public static ImageSource? ToThumbnail(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? new BitmapImage(uri) { DecodePixelWidth = ThumbnailDecodeWidth }
            : null;
}
