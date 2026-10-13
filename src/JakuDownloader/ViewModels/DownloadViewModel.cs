using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;
using JakuDownloader.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace JakuDownloader.ViewModels;

public sealed partial class DownloadViewModel(
    DownloadService downloadService,
    VideoInfoService videoInfoService,
    NotificationService notificationService,
    ToolLocator toolLocator) : ObservableObject
{
    private const string UnexpectedErrorMessage =
        "Ocurrió un error inesperado. Podés volver a intentar o copiar los detalles para reportarlo.";

    private const string CollectionMessage =
        "Es un link de una lista de reproducción o de un canal. Por ahora se baja de a un video: abrí el video que quieras y pegá su link. Las listas y canales llegan en la v00.07.";

    // Espera a que se termine de escribir o pegar el link antes de consultar a yt-dlp
    private static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(500);

    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _previewCancellation;
    private string? _lastFilePath;
    private DownloadStage? _announcedStage;

    /// <summary>
    /// Pide a la vista que anuncie un cambio importante a los lectores de pantalla.
    /// Solo cambios de etapa, nunca cada porcentaje.
    /// </summary>
    public event EventHandler<string>? AnnouncementRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(AreCodecOptionsEnabled))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(PasteCommand))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial bool IsCancelling { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial string StageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsResultOpen { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity ResultSeverity { get; set; }

    [ObservableProperty]
    public partial string ResultTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResultDetails))]
    public partial string ResultDetails { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsResultDetailsVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResultAction))]
    [NotifyPropertyChangedFor(nameof(ResultActionText))]
    public partial ResultAction ResultAction { get; set; }

    // Valores en el mismo orden que los items de cada ComboBox de la vista
    private static readonly VideoContainer[] VideoContainers = [VideoContainer.Mp4, VideoContainer.Mkv, VideoContainer.Webm];
    private static readonly AudioFormat[] AudioFormats = [AudioFormat.Mp3, AudioFormat.M4a, AudioFormat.Opus, AudioFormat.Flac, AudioFormat.Wav];
    private static readonly int?[] Resolutions = [null, 2160, 1440, 1080, 720, 480, 360];
    private static readonly VideoCodec[] VideoCodecs = [VideoCodec.Auto, VideoCodec.H264, VideoCodec.Vp9, VideoCodec.Av1];
    private static readonly AudioCodec[] AudioCodecs = [AudioCodec.Auto, AudioCodec.Aac, AudioCodec.Opus];

    private DownloadOptions? _activeOptions;

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

    public bool AreCodecOptionsEnabled => IsIdle && !IsWebm;

    public string CodecsDescription => IsWebm
        ? "WebM siempre usa VP9 y Opus"
        : "Si el video no está en ese códec, se usa el mejor disponible";

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

    public DownloadOptions CurrentOptions => new(
        IsAudio ? MediaKind.Audio : MediaKind.Video,
        Pick(VideoContainers, VideoFormatIndex),
        Pick(AudioFormats, AudioFormatIndex),
        Pick(Resolutions, ResolutionIndex),
        Pick(VideoCodecs, VideoCodecIndex),
        Pick(AudioCodecs, AudioCodecIndex));

    // Un ComboBox puede informar -1 mientras cambia; se toma la primera opcion
    private static T Pick<T>(T[] values, int index) => values[Math.Clamp(index, 0, values.Length - 1)];

    public bool IsIdle => !IsDownloading;

    public string DownloadDirectory { get; } = AppPaths.DownloadDirectory;

    public bool HasResultDetails => ResultDetails.Length > 0;

    public bool HasResultAction => ResultAction != ResultAction.None;

    public string ResultActionText => ResultAction switch
    {
        ResultAction.OpenFolder => "Abrir carpeta",
        ResultAction.Retry => "Reintentar",
        ResultAction.OpenToolsFolder => "Abrir carpeta tools",
        _ => string.Empty,
    };

    // Con un link de lista o canal el boton queda deshabilitado: la vista previa explica por que
    private bool CanDownload()
        => !IsDownloading
            && !string.IsNullOrWhiteSpace(Url)
            && !(UrlValidator.TryParse(Url, out var uri) && UrlValidator.IsCollection(uri));

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
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

        var missingTools = toolLocator.GetMissingTools();
        if (missingTools.Count > 0)
        {
            ShowResult(InfoBarSeverity.Error, "Faltan herramientas",
                $"No se encontró {string.Join(" ni ", missingTools)} en la carpeta tools, junto al programa. "
                + "Volvé a descomprimir Jaku Downloader completo; si compilaste la app, corré scripts\\get-tools.ps1.",
                action: ResultAction.OpenToolsFolder);
            return;
        }

        var outputDirectory = DownloadDirectory;
        _announcedStage = null;
        IsDownloading = true;
        ApplyProgress(new DownloadProgress(DownloadStage.FetchingInfo));
        _cancellation = new CancellationTokenSource();

        try
        {
            // Dentro del try: un destino sin permisos o desconectado tiene que terminar en un mensaje
            Directory.CreateDirectory(outputDirectory);
            notificationService.Show("Descarga iniciada", $"El archivo se va a guardar en {outputDirectory}", outputDirectory);

            // Progress<T> devuelve los reportes al hilo de la interfaz
            var progress = new Progress<DownloadProgress>(ApplyProgress);
            _activeOptions = CurrentOptions;
            var result = await downloadService.DownloadAsync(uri, outputDirectory, _activeOptions, progress, _cancellation.Token);

            if (result.Success)
            {
                var fileName = Path.GetFileName(result.FilePath) ?? string.Empty;
                _lastFilePath = result.FilePath;
                ShowResult(InfoBarSeverity.Success, "Descarga completa", fileName, action: ResultAction.OpenFolder);
                notificationService.Show("Descarga completa", fileName, outputDirectory);
                Url = string.Empty;
            }
            else
            {
                ShowResult(InfoBarSeverity.Error, "No se pudo descargar",
                    result.ErrorMessage ?? UnexpectedErrorMessage,
                    details: result.ErrorDetails,
                    showDetails: result.ErrorMessage is null,
                    action: ResultAction.Retry);
            }
        }
        catch (OperationCanceledException)
        {
            ShowResult(InfoBarSeverity.Informational, "Descarga cancelada", "Se borraron los archivos parciales.");
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowResult(InfoBarSeverity.Error, "No se puede escribir en la carpeta de destino",
                $"Revisá los permisos de {outputDirectory} y volvé a intentar.",
                details: ex.Message, action: ResultAction.Retry);
        }
        catch (IOException ex)
        {
            ShowResult(InfoBarSeverity.Error, "La carpeta de destino no está disponible",
                "Comprobá que la unidad esté conectada y tenga espacio, y volvé a intentar.",
                details: ex.Message, action: ResultAction.Retry);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, "No se pudo descargar", UnexpectedErrorMessage,
                details: ex.Message, showDetails: true, action: ResultAction.Retry);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsDownloading = false;
            IsCancelling = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsDownloading))]
    private void Cancel()
    {
        if (IsCancelling) return;

        IsCancelling = true;
        StageText = "Cancelando...";
        DetailsText = string.Empty;
        IsProgressIndeterminate = true;
        AnnouncementRequested?.Invoke(this, "Cancelando la descarga");
        _cancellation?.Cancel();
    }

    /// <summary>
    /// Cancela la descarga en curso, si hay una, y espera a que termine de limpiar.
    /// </summary>
    public async Task CancelAndWaitAsync()
    {
        if (IsDownloading) Cancel();

        if (DownloadCommand.ExecutionTask is { } task)
        {
            try { await task; }
            catch (Exception) { }
        }
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task PasteAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text)) return;

            var text = (await content.GetTextAsync()).Trim();

            // Si mientras se leia el portapapeles empezo una descarga, no se toca el link
            if (!IsDownloading) Url = text;
        }
        catch (Exception)
        {
            ShowResult(InfoBarSeverity.Warning, "No se pudo leer el portapapeles",
                "Probá pegar el link con Ctrl+V en la caja de texto.");
        }
    }

    [RelayCommand]
    private void ExecuteResultAction()
    {
        switch (ResultAction)
        {
            case ResultAction.OpenFolder:
                if (_lastFilePath is not null)
                {
                    if (!ShellHelper.TryShowInFolder(_lastFilePath, out var error)) ShowFolderError(error);
                }
                else
                {
                    OpenDownloadFolder();
                }
                break;

            case ResultAction.Retry:
                if (DownloadCommand.CanExecute(null)) DownloadCommand.Execute(null);
                break;

            case ResultAction.OpenToolsFolder:
                if (!ShellHelper.TryOpenFolder(toolLocator.ToolsDirectory, out var toolsError)) ShowFolderError(toolsError);
                break;
        }
    }

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        if (!ShellHelper.TryOpenFolder(DownloadDirectory, out var error)) ShowFolderError(error);
    }

    [RelayCommand]
    private void CopyDetails()
    {
        try
        {
            var package = new DataPackage();
            package.SetText(ResultDetails);
            Clipboard.SetContent(package);
        }
        catch (Exception)
        {
            // Los detalles siguen visibles y seleccionables en el cartel
            IsResultDetailsVisible = true;
        }
    }

    public void CancelDownload()
    {
        _cancellation?.Cancel();
        _previewCancellation?.Cancel();
    }

    partial void OnUrlChanged(string value)
    {
        // Un resultado anterior no corresponde al link nuevo. Si el link se vacio (por ejemplo
        // despues de una descarga exitosa) el cartel se deja, porque es el resultado de esa descarga
        if (value.Length > 0 && !IsDownloading) IsResultOpen = false;

        RefreshPreview(value);
    }

    /// <summary>
    /// Busca la informacion del video cada vez que cambia el link. Una busqueda nueva cancela la anterior.
    /// </summary>
    private async void RefreshPreview(string text)
    {
        _previewCancellation?.Cancel();
        _previewCancellation = null;

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

        if (toolLocator.GetMissingTools().Count > 0)
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
            var result = await videoInfoService.GetInfoAsync(uri, cancellation.Token);
            if (cancellation.IsCancellationRequested) return;

            if (result.Info is { } info)
            {
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

    private void ApplyProgress(DownloadProgress progress)
    {
        // Puede llegar un reporte atrasado despues de terminar o mientras se cancela
        if (!IsDownloading || IsCancelling) return;

        var stage = progress.Stage switch
        {
            DownloadStage.FetchingInfo => "Obteniendo información del video...",
            DownloadStage.DownloadingVideo => "Descargando video",
            DownloadStage.DownloadingAudio => "Descargando audio",
            DownloadStage.Downloading => "Descargando",
            DownloadStage.Merging => "Uniendo video y audio...",
            DownloadStage.ConvertingAudio when _activeOptions is { ConvertsAudio: true } options
                => $"Convirtiendo a {options.Extension.ToUpperInvariant()}...",
            DownloadStage.ConvertingAudio => "Preparando el audio...",
            _ => "Procesando...",
        };

        if (progress.Stage != _announcedStage)
        {
            _announcedStage = progress.Stage;
            AnnouncementRequested?.Invoke(this, stage.TrimEnd('.'));
        }

        if (progress.Percent is double percent)
        {
            IsProgressIndeterminate = false;
            ProgressValue = percent;
            StageText = $"{stage} · {percent:0} %";
        }
        else
        {
            IsProgressIndeterminate = true;
            StageText = stage;
        }

        DetailsText = BuildDetails(progress);
    }

    private static string BuildDetails(DownloadProgress progress)
    {
        var parts = new List<string>();
        if (progress.DownloadedBytes is long done && progress.TotalBytes is long total)
        {
            parts.Add($"{Formatters.FormatBytes(done)} de {Formatters.FormatBytes(total)}");
        }
        if (progress.BytesPerSecond is double speed)
        {
            parts.Add($"{Formatters.FormatBytes(speed)}/s");
        }
        if (progress.SecondsRemaining is int seconds && progress.Percent < 100)
        {
            parts.Add($"quedan {Formatters.FormatDuration(seconds)}");
        }

        return string.Join(" · ", parts);
    }

    private void ShowFolderError(string message)
        => ShowResult(InfoBarSeverity.Warning, "No se pudo abrir la carpeta", message);

    private void ShowResult(
        InfoBarSeverity severity,
        string title,
        string message,
        string? details = null,
        bool showDetails = false,
        ResultAction action = ResultAction.None)
    {
        ResultSeverity = severity;
        ResultTitle = title;
        ResultMessage = message;
        ResultDetails = details ?? string.Empty;
        IsResultDetailsVisible = showDetails && !string.IsNullOrEmpty(details);
        ResultAction = action;
        IsResultOpen = true;
    }
}
