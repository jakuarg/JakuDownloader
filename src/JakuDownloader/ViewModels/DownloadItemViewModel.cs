using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.ViewModels;

/// <summary>
/// Una descarga de la cola: su estado, su progreso y las acciones que admite en cada momento.
/// </summary>
public sealed partial class DownloadItemViewModel : ObservableObject
{
    private readonly DownloadQueueViewModel _queue;

    public DownloadItemViewModel(DownloadQueueViewModel queue, Uri url, DownloadOptions options, string outputDirectory, VideoInfo? preview)
    {
        _queue = queue;
        Url = url;
        Options = options;
        OutputDirectory = outputDirectory;
        Title = preview?.Title ?? url.AbsoluteUri;
        Channel = preview?.Channel;
        ThumbnailUrl = preview?.ThumbnailUrl;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public Uri Url { get; }

    public DownloadOptions Options { get; }

    /// <summary>
    /// Destino elegido al agregarla: cambiar la carpeta en Configuracion no mueve las que ya estan en la cola.
    /// </summary>
    public string OutputDirectory { get; }

    /// <summary>
    /// Orden de llegada: la cola arranca primero las mas viejas. Es un contador y no la hora porque
    /// los videos de una lista se agregan todos en el mismo instante.
    /// </summary>
    public long Sequence { get; private set; } = Interlocked.Increment(ref s_nextSequence);

    private static long s_nextSequence;

    /// <summary>
    /// Carpeta con los archivos parciales. Se conserva al pausar para retomar desde ahi.
    /// </summary>
    internal string? WorkDirectory { get; set; }

    internal CancellationTokenSource? Cancellation { get; set; }

    internal bool PauseRequested { get; set; }

    internal bool RemoveRequested { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    public partial string? Channel { get; set; }

    [ObservableProperty]
    public partial string? ThumbnailUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsDownloading))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(IsNotFailed))]
    [NotifyPropertyChangedFor(nameof(IsPaused))]
    [NotifyPropertyChangedFor(nameof(ShowProgress))]
    [NotifyPropertyChangedFor(nameof(CanPause))]
    [NotifyPropertyChangedFor(nameof(CanResume))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    [NotifyPropertyChangedFor(nameof(CanOpen))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    [NotifyPropertyChangedFor(nameof(HasErrorDetails))]
    public partial DownloadStatus Status { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string StageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailsText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorDetails))]
    public partial string? ErrorDetails { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string? FilePath { get; set; }

    public string Subtitle => Channel is null ? Options.Label : $"{Channel} · {Options.Label}";

    public string StatusText => Status switch
    {
        DownloadStatus.Queued => "En cola",
        DownloadStatus.Downloading => StageText,
        DownloadStatus.Paused => ProgressValue > 0 ? $"En pausa · {ProgressValue:0} %" : "En pausa",
        DownloadStatus.Completed => $"Completa · {Path.GetFileName(FilePath)}",
        DownloadStatus.Failed => ErrorMessage,
        _ => "Cancelada",
    };

    public bool IsDownloading => Status == DownloadStatus.Downloading;

    public bool IsPaused => Status == DownloadStatus.Paused;

    public bool IsFailed => Status == DownloadStatus.Failed;

    public bool IsNotFailed => !IsFailed;

    public bool ShowProgress => Status is DownloadStatus.Downloading or DownloadStatus.Paused;

    public bool CanPause => Status is DownloadStatus.Queued or DownloadStatus.Downloading;

    public bool CanResume => Status == DownloadStatus.Paused;

    public bool CanCancel => Status is DownloadStatus.Queued or DownloadStatus.Downloading or DownloadStatus.Paused;

    public bool CanRetry => Status is DownloadStatus.Failed or DownloadStatus.Canceled;

    public bool CanOpen => Status == DownloadStatus.Completed;

    public bool CanRemove => Status != DownloadStatus.Downloading;

    public bool HasErrorDetails => IsFailed && !string.IsNullOrEmpty(ErrorDetails);

    [RelayCommand]
    private void Pause() => _queue.Pause(this);

    [RelayCommand]
    private void Resume() => _queue.Resume(this);

    [RelayCommand]
    private void Cancel() => _queue.Cancel(this);

    [RelayCommand]
    private void Retry() => _queue.Retry(this);

    [RelayCommand]
    private void Remove() => _queue.Remove(this);

    [RelayCommand]
    private void OpenFile() => _queue.OpenFile(this);

    [RelayCommand]
    private void ShowInFolder() => _queue.ShowInFolder(this);

    [RelayCommand]
    private void CopyDetails() => _queue.CopyText(ErrorDetails ?? string.Empty);

    internal void BeginDownload()
    {
        PauseRequested = false;
        ErrorMessage = string.Empty;
        ErrorDetails = null;
        DetailsText = string.Empty;
        Status = DownloadStatus.Downloading;
        ApplyProgress(new DownloadProgress(DownloadStage.FetchingInfo));
    }

    internal void Requeue(bool moveToBack)
    {
        if (moveToBack) Sequence = Interlocked.Increment(ref s_nextSequence);
        Status = DownloadStatus.Queued;
    }

    internal void MarkPaused()
    {
        DetailsText = string.Empty;
        Status = DownloadStatus.Paused;
    }

    internal void MarkCanceled()
    {
        ProgressValue = 0;
        DetailsText = string.Empty;
        Status = DownloadStatus.Canceled;
    }

    internal void Complete(string filePath)
    {
        FilePath = filePath;
        ProgressValue = 100;
        DetailsText = string.Empty;
        Status = DownloadStatus.Completed;
    }

    internal void Fail(string message, string? details)
    {
        ErrorMessage = message;
        ErrorDetails = details;
        ProgressValue = 0;
        DetailsText = string.Empty;
        Status = DownloadStatus.Failed;
    }

    internal void ApplyInfo(VideoInfo info)
    {
        Title = info.Title;
        Channel = info.Channel;
        if (info.ThumbnailUrl is not null) ThumbnailUrl = info.ThumbnailUrl;
    }

    internal void ApplyProgress(DownloadProgress progress)
    {
        // Puede llegar un reporte atrasado despues de pausar o cancelar
        if (Status != DownloadStatus.Downloading || Cancellation?.IsCancellationRequested == true) return;

        var stage = progress.Stage switch
        {
            DownloadStage.FetchingInfo => "Obteniendo información del video...",
            DownloadStage.DownloadingVideo => "Descargando video",
            DownloadStage.DownloadingAudio => "Descargando audio",
            DownloadStage.Downloading => "Descargando",
            DownloadStage.Merging => "Uniendo video y audio...",
            DownloadStage.ConvertingAudio when Options.ConvertsAudio => $"Convirtiendo a {Options.Extension.ToUpperInvariant()}...",
            DownloadStage.ConvertingAudio => "Preparando el audio...",
            _ => "Procesando...",
        };

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
}
