using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;
using JakuDownloader.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace JakuDownloader.ViewModels;

/// <summary>
/// Formulario de la pagina Descargar: link, vista previa (de un video o de una lista) y opciones.
/// Las descargas en si las maneja la cola.
/// </summary>
public sealed partial class DownloadViewModel : ObservableObject
{
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
    [NotifyPropertyChangedFor(nameof(PlaylistFolderHint))]
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

    // Tipo de link

    /// <summary>
    /// Link de una lista o un canal: siempre se baja como lista.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaylistMode))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsCollectionUrl { get; set; }

    /// <summary>
    /// Video abierto desde una lista: se baja solo, salvo que se pida la lista completa.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaylistMode))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsVideoInPlaylist { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaylistMode))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool DownloadWholePlaylist { get; set; }

    public bool IsPlaylistMode => IsCollectionUrl || (IsVideoInPlaylist && DownloadWholePlaylist);

    // Vista previa

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewVisible))]
    [NotifyPropertyChangedFor(nameof(IsPreviewLoading))]
    [NotifyPropertyChangedFor(nameof(IsVideoPreviewReady))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistPreviewReady))]
    [NotifyPropertyChangedFor(nameof(IsPreviewFailed))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial PreviewState PreviewState { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoPreviewReady))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistPreviewReady))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsPlaylistPreview { get; set; }

    [ObservableProperty]
    public partial string PreviewLoadingText { get; set; } = string.Empty;

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

    public bool IsVideoPreviewReady => PreviewState == PreviewState.Ready && !IsPlaylistPreview;

    public bool IsPlaylistPreviewReady => PreviewState == PreviewState.Ready && IsPlaylistPreview;

    public bool IsPreviewFailed => PreviewState == PreviewState.Failed;

    // Lista

    public ObservableCollection<PlaylistEntryViewModel> PlaylistEntries { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaylistFolderHint))]
    public partial string PlaylistTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlaylistSubtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? PlaylistThumbnailUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial int SelectedCount { get; set; }

    /// <summary>
    /// Guardar los videos de la lista en una subcarpeta con su nombre, para no mezclarlos con el resto.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaylistFolderHint))]
    public partial bool SaveInPlaylistFolder { get; set; } = true;

    public string SelectionSummary => $"{SelectedCount} de {PlaylistEntries.Count} videos elegidos";

    public string PlaylistFolderHint => SaveInPlaylistFolder
        ? $"En {Path.Combine(DownloadDirectory, FolderName.FromTitle(PlaylistTitle))}"
        : $"En {DownloadDirectory}";

    private bool CanDownload()
    {
        if (string.IsNullOrWhiteSpace(Url)) return false;

        // Una lista se puede agregar recien cuando se cargaron sus videos y hay alguno elegido
        return !IsPlaylistMode || (IsPlaylistPreviewReady && SelectedCount > 0);
    }

    /// <summary>
    /// Agrega a la cola el video, o los videos elegidos de la lista, y deja el formulario listo para el siguiente.
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

        var missingTools = _toolLocator.GetMissingRequired();
        if (missingTools.Count > 0)
        {
            ShowResult(InfoBarSeverity.Error, "Faltan herramientas",
                $"Para descargar hace falta {string.Join(" y ", missingTools.Select(ToolLocator.DisplayName))}. "
                + "Usá el botón \"Descargar herramientas\" de arriba.");
            return;
        }

        if (IsPlaylistMode)
        {
            EnqueuePlaylist();
            return;
        }

        // Si la vista previa ya cargo, la cola muestra titulo y miniatura desde el principio
        var preview = _preview is { } loaded && loaded.Url == uri ? loaded.Info : null;
        var item = Queue.Enqueue(uri, CurrentOptions, DownloadDirectory, preview);
        AnnouncementRequested?.Invoke(this, $"Agregado a la cola: {item.Title}");
        Url = string.Empty;
    }

    private void EnqueuePlaylist()
    {
        var selected = PlaylistEntries.Where(entry => entry.IsSelected).Select(entry => entry.Entry).ToList();
        if (selected.Count == 0) return;

        var outputDirectory = SaveInPlaylistFolder
            ? Path.Combine(DownloadDirectory, FolderName.FromTitle(PlaylistTitle))
            : DownloadDirectory;
        var options = CurrentOptions;

        // En el orden de la lista: la cola arranca primero las que se agregaron antes
        foreach (var entry in selected)
        {
            var preview = new VideoInfo(entry.Id, entry.Title, null, entry.DurationSeconds, entry.ThumbnailUrl, false, false);
            Queue.Enqueue(new Uri(entry.Url), options, outputDirectory, preview);
        }

        var message = selected.Count == 1 ? "Se agregó 1 video a la cola" : $"Se agregaron {selected.Count} videos a la cola";
        ShowResult(InfoBarSeverity.Informational, message, $"De \"{PlaylistTitle}\". Se guardan en {outputDirectory}");
        AnnouncementRequested?.Invoke(this, message);
        Url = string.Empty;
    }

    [RelayCommand]
    private void SelectAllEntries()
    {
        foreach (var entry in PlaylistEntries) entry.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoEntries()
    {
        foreach (var entry in PlaylistEntries) entry.IsSelected = false;
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

    partial void OnUrlChanged(string value)
    {
        // Un resultado anterior no corresponde al link nuevo. Si el link se vacio (por ejemplo
        // al agregarlo a la cola) el cartel se deja
        if (value.Length > 0) IsResultOpen = false;

        RefreshPreview(value);
    }

    partial void OnDownloadWholePlaylistChanged(bool value) => RefreshPreview(Url);

    /// <summary>
    /// Vuelve a buscar la vista previa del link actual, por ejemplo despues de instalar las herramientas.
    /// </summary>
    public void ReloadPreview() => RefreshPreview(Url);

    /// <summary>
    /// Busca la informacion del video o de la lista cada vez que cambia el link. Una busqueda nueva cancela la anterior.
    /// </summary>
    private async void RefreshPreview(string text)
    {
        _previewCancellation?.Cancel();
        _previewCancellation = null;
        _preview = null;

        if (!UrlValidator.TryParse(text, out var uri))
        {
            IsCollectionUrl = false;
            IsVideoInPlaylist = false;
            PreviewState = PreviewState.None;
            return;
        }

        IsCollectionUrl = UrlValidator.IsCollection(uri);
        IsVideoInPlaylist = UrlValidator.IsVideoInPlaylist(uri);

        if (_toolLocator.GetMissingRequired().Count > 0)
        {
            PreviewState = PreviewState.None;
            return;
        }

        var loadPlaylist = IsPlaylistMode;
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewLoadingText = loadPlaylist ? "Buscando los videos de la lista..." : "Buscando información del video...";
        PreviewState = PreviewState.Loading;

        try
        {
            await Task.Delay(PreviewDelay, cancellation.Token);

            if (loadPlaylist)
            {
                var result = await _videoInfoService.GetPlaylistAsync(UrlValidator.ToPlaylistUrl(uri), cancellation.Token);
                if (cancellation.IsCancellationRequested) return;

                if (result.Info is { Entries.Count: > 0 } playlist)
                {
                    ShowPlaylist(playlist);
                    AnnouncementRequested?.Invoke(this, $"Lista encontrada: {playlist.Title}, {playlist.Entries.Count} videos");
                }
                else
                {
                    ShowPreviewError(result.ErrorMessage ?? "La lista no tiene videos que se puedan bajar.");
                }
            }
            else
            {
                var result = await _videoInfoService.GetInfoAsync(uri, cancellation.Token);
                if (cancellation.IsCancellationRequested) return;

                if (result.Info is { } info)
                {
                    _preview = (uri, info);
                    ShowVideo(info);
                    AnnouncementRequested?.Invoke(this, $"Video encontrado: {info.Title}");
                }
                else
                {
                    ShowPreviewError(result.ErrorMessage ?? string.Empty);
                }
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
                ShowPreviewError(loadPlaylist ? "No se pudo obtener la lista de videos." : "No se pudo obtener la información del video.");
            }
        }
        finally
        {
            if (_previewCancellation == cancellation) _previewCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ShowVideo(VideoInfo info)
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
        IsPlaylistPreview = false;
        PreviewState = PreviewState.Ready;
    }

    private void ShowPlaylist(PlaylistInfo playlist)
    {
        PlaylistEntries.Clear();
        foreach (var entry in playlist.Entries)
        {
            PlaylistEntries.Add(new PlaylistEntryViewModel(entry, UpdateSelectedCount));
        }

        PlaylistTitle = playlist.Title;
        PlaylistThumbnailUrl = playlist.Entries[0].ThumbnailUrl;

        var count = playlist.Entries.Count == 1 ? "1 video" : $"{playlist.Entries.Count} videos";
        if (playlist.IsTruncated) count += $" (se cargaron los primeros {VideoInfoService.MaxPlaylistEntries})";
        PlaylistSubtitle = playlist.Channel is null ? count : $"{playlist.Channel} · {count}";

        UpdateSelectedCount();
        IsPlaylistPreview = true;
        PreviewState = PreviewState.Ready;
    }

    private void ShowPreviewError(string message)
    {
        PreviewMessage = message;
        IsPlaylistPreview = false;
        PreviewState = PreviewState.Failed;
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = PlaylistEntries.Count(entry => entry.IsSelected);

        // El total tambien cambia al cargar otra lista, aunque la cantidad elegida sea la misma
        OnPropertyChanged(nameof(SelectionSummary));
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
