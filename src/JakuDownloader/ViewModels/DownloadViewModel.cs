using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;
using JakuDownloader.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace JakuDownloader.ViewModels;

/// <summary>
/// Formulario de la pagina Descargar: link, vista previa y opciones. Las descargas en si las maneja la cola.
/// </summary>
public sealed partial class DownloadViewModel : ObservableObject
{
    private const string CollectionMessage =
        "Es un link de una lista de reproducción o de un canal. Por ahora se baja de a un video: abrí el video que quieras y pegá su link. Las listas y canales llegan en la v00.07.";

    // Espera a que se termine de escribir o pegar el link antes de consultar a yt-dlp
    private static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(500);

    // Valores en el mismo orden que los items de cada ComboBox de la vista
    private static readonly VideoContainer[] VideoContainers = [VideoContainer.Mp4, VideoContainer.Mkv, VideoContainer.Webm];
    private static readonly AudioFormat[] AudioFormats = [AudioFormat.Mp3, AudioFormat.M4a, AudioFormat.Opus, AudioFormat.Flac, AudioFormat.Wav];
    private static readonly int?[] Resolutions = [null, 2160, 1440, 1080, 720, 480, 360];
    private static readonly VideoCodec[] VideoCodecs = [VideoCodec.Auto, VideoCodec.H264, VideoCodec.Vp9, VideoCodec.Av1];
    private static readonly AudioCodec[] AudioCodecs = [AudioCodec.Auto, AudioCodec.Aac, AudioCodec.Opus];

    private readonly VideoInfoService _videoInfoService;
    private readonly ToolLocator _toolLocator;

    private CancellationTokenSource? _previewCancellation;
    private (Uri Url, VideoInfo Info)? _preview;
    private string? _lastFilePath;

    public DownloadViewModel(VideoInfoService videoInfoService, ToolLocator toolLocator, SettingsService settings, DownloadQueueViewModel queue)
    {
        _videoInfoService = videoInfoService;
        _toolLocator = toolLocator;
        Queue = queue;

        // La carpeta se puede cambiar desde Configuracion; lo proximo que se agregue a la cola usa la nueva
        DownloadDirectory = settings.DownloadDirectory;
        settings.Changed += (_, _) => DownloadDirectory = settings.DownloadDirectory;

        queue.ItemCompleted += (_, filePath) =>
        {
            _lastFilePath = filePath;
            ShowResult(InfoBarSeverity.Success, "Descarga completa", Path.GetFileName(filePath), showOpenFolder: true);
        };
    }

    public DownloadQueueViewModel Queue { get; }

    /// <summary>
    /// Pide a la vista que anuncie un cambio importante a los lectores de pantalla.
    /// </summary>
    public event EventHandler<string>? AnnouncementRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DownloadDirectory { get; set; }

    // Resultado: errores al agregar a la cola y la ultima descarga completa

    [ObservableProperty]
    public partial bool IsResultOpen { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity ResultSeverity { get; set; }

    [ObservableProperty]
    public partial string ResultTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanOpenResultFolder { get; set; }

    // Opciones

    /// <summary>
    /// 0: video, 1: solo audio. Indice para el ComboBox.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideo))]
    [NotifyPropertyChangedFor(nameof(IsAudio))]
    public partial int MediaKindIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreCodecOptionsEnabled))]
    [NotifyPropertyChangedFor(nameof(CodecsDescription))]
    public partial int VideoFormatIndex { get; set; }

    [ObservableProperty]
    public partial int AudioFormatIndex { get; set; }

    [ObservableProperty]
    public partial int ResolutionIndex { get; set; }

    [ObservableProperty]
    public partial int VideoCodecIndex { get; set; }

    [ObservableProperty]
    public partial int AudioCodecIndex { get; set; }

    public bool IsVideo => MediaKindIndex == 0;

    public bool IsAudio => !IsVideo;

    private bool IsWebm => Pick(VideoContainers, VideoFormatIndex) == VideoContainer.Webm;

    public bool AreCodecOptionsEnabled => !IsWebm;

    public string CodecsDescription => IsWebm
        ? "WebM siempre usa VP9 y Opus"
        : "Si el video no está en ese códec, se usa el mejor disponible";

    public DownloadOptions CurrentOptions => new(
        IsAudio ? MediaKind.Audio : MediaKind.Video,
        Pick(VideoContainers, VideoFormatIndex),
        Pick(AudioFormats, AudioFormatIndex),
        Pick(Resolutions, ResolutionIndex),
        Pick(VideoCodecs, VideoCodecIndex),
        Pick(AudioCodecs, AudioCodecIndex));

    // Un ComboBox puede informar -1 mientras cambia; se toma la primera opcion
    private static T Pick<T>(T[] values, int index) => values[Math.Clamp(index, 0, values.Length - 1)];

    // Vista previa

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewVisible))]
    [NotifyPropertyChangedFor(nameof(IsPreviewLoading))]
    [NotifyPropertyChangedFor(nameof(IsPreviewReady))]
    [NotifyPropertyChangedFor(nameof(IsPreviewFailed))]
    public partial PreviewState PreviewState { get; set; }

    [ObservableProperty]
    public partial string PreviewTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreviewChannel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreviewDuration { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? PreviewThumbnailUrl { get; set; }

    [ObservableProperty]
    public partial string PreviewMessage { get; set; } = string.Empty;

    public bool IsPreviewVisible => PreviewState != PreviewState.None;

    public bool IsPreviewLoading => PreviewState == PreviewState.Loading;

    public bool IsPreviewReady => PreviewState == PreviewState.Ready;

    public bool IsPreviewFailed => PreviewState == PreviewState.Failed;

    // Con un link de lista o canal el boton queda deshabilitado: la vista previa explica por que
    private bool CanDownload()
        => !string.IsNullOrWhiteSpace(Url)
            && !(UrlValidator.TryParse(Url, out var uri) && UrlValidator.IsCollection(uri));

    /// <summary>
    /// Agrega el link a la cola con las opciones elegidas y deja el formulario listo para el siguiente.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDownload))]
    private void Download()
    {
        IsResultOpen = false;

        if (!UrlValidator.TryParse(Url, out var uri))
        {
            ShowResult(InfoBarSeverity.Error, "El link no es válido",
                "Revisá que sea un link completo de YouTube, por ejemplo https://www.youtube.com/watch?v=...");
            return;
        }

        if (UrlValidator.IsCollection(uri))
        {
            ShowResult(InfoBarSeverity.Warning, "Las listas y canales todavía no se pueden bajar", CollectionMessage);
            return;
        }

        var missingTools = _toolLocator.GetMissingRequired();
        if (missingTools.Count > 0)
        {
            ShowResult(InfoBarSeverity.Error, "Faltan herramientas",
                $"Para descargar hace falta {string.Join(" y ", missingTools.Select(ToolLocator.DisplayName))}. "
                + "Usá el botón \"Descargar herramientas\" de arriba.");
            return;
        }

        // Si la vista previa ya cargo, la cola muestra titulo y miniatura desde el principio
        var preview = _preview is { } loaded && loaded.Url == uri ? loaded.Info : null;
        var item = Queue.Enqueue(uri, CurrentOptions, DownloadDirectory, preview);
        AnnouncementRequested?.Invoke(this, $"Agregado a la cola: {item.Title}");
        Url = string.Empty;
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text)) return;
            Url = (await content.GetTextAsync()).Trim();
        }
        catch (Exception)
        {
            ShowResult(InfoBarSeverity.Warning, "No se pudo leer el portapapeles",
                "Probá pegar el link con Ctrl+V en la caja de texto.");
        }
    }

    [RelayCommand]
    private void OpenResultFolder()
    {
        var opened = _lastFilePath is not null
            ? ShellHelper.TryShowInFolder(_lastFilePath, out var error)
            : ShellHelper.TryOpenFolder(DownloadDirectory, out error);
        if (!opened) ShowResult(InfoBarSeverity.Warning, "No se pudo abrir la carpeta", error!);
    }

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        if (!ShellHelper.TryOpenFolder(DownloadDirectory, out var error))
        {
            ShowResult(InfoBarSeverity.Warning, "No se pudo abrir la carpeta", error);
        }
    }

    public void CancelPreview() => _previewCancellation?.Cancel();

    partial void OnUrlChanged(string value)
    {
        // Un resultado anterior no corresponde al link nuevo. Si el link se vacio (por ejemplo
        // al agregarlo a la cola) el cartel se deja
        if (value.Length > 0) IsResultOpen = false;

        RefreshPreview(value);
    }

    /// <summary>
    /// Vuelve a buscar la vista previa del link actual, por ejemplo despues de instalar las herramientas.
    /// </summary>
    public void ReloadPreview() => RefreshPreview(Url);

    /// <summary>
    /// Busca la informacion del video cada vez que cambia el link. Una busqueda nueva cancela la anterior.
    /// </summary>
    private async void RefreshPreview(string text)
    {
        _previewCancellation?.Cancel();
        _previewCancellation = null;
        _preview = null;

        if (!UrlValidator.TryParse(text, out var uri))
        {
            PreviewState = PreviewState.None;
            return;
        }

        if (UrlValidator.IsCollection(uri))
        {
            PreviewMessage = CollectionMessage;
            PreviewState = PreviewState.Failed;
            return;
        }

        if (_toolLocator.GetMissingRequired().Count > 0)
        {
            PreviewState = PreviewState.None;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewState = PreviewState.Loading;

        try
        {
            await Task.Delay(PreviewDelay, cancellation.Token);
            var result = await _videoInfoService.GetInfoAsync(uri, cancellation.Token);
            if (cancellation.IsCancellationRequested) return;

            if (result.Info is { } info)
            {
                _preview = (uri, info);
                ShowPreview(info);
                AnnouncementRequested?.Invoke(this, $"Video encontrado: {info.Title}");
            }
            else
            {
                PreviewMessage = result.ErrorMessage ?? string.Empty;
                PreviewState = PreviewState.Failed;
            }
        }
        catch (OperationCanceledException)
        {
            // La reemplazo otra busqueda o se borro el link
        }
        catch (Exception)
        {
            if (!cancellation.IsCancellationRequested)
            {
                PreviewMessage = "No se pudo obtener la información del video.";
                PreviewState = PreviewState.Failed;
            }
        }
        finally
        {
            if (_previewCancellation == cancellation) _previewCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ShowPreview(VideoInfo info)
    {
        PreviewTitle = info.Title;
        PreviewChannel = info.Channel ?? "Canal desconocido";
        PreviewDuration = info switch
        {
            { IsLive: true } => "En vivo: se graba mientras dure la transmisión",
            { IsUpcoming: true } => "Todavía no se estrenó",
            { DurationSeconds: int seconds } => Formatters.FormatDuration(seconds),
            _ => "Duración desconocida",
        };
        PreviewThumbnailUrl = info.ThumbnailUrl;
        PreviewState = PreviewState.Ready;
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message, bool showOpenFolder = false)
    {
        ResultSeverity = severity;
        ResultTitle = title;
        ResultMessage = message;
        CanOpenResultFolder = showOpenFolder;
        IsResultOpen = true;
    }
}
