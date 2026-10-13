using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;
using JakuDownloader.Services;
using Windows.ApplicationModel.DataTransfer;

namespace JakuDownloader.ViewModels;

/// <summary>
/// Cola de descargas: arranca hasta N a la vez (Configuracion) y maneja pausar, reanudar, cancelar,
/// reintentar y quitar. Todo corre en el hilo de la interfaz; las descargas en si son procesos aparte.
/// </summary>
public sealed partial class DownloadQueueViewModel : ObservableObject
{
    private const string UnexpectedErrorMessage =
        "Ocurrió un error inesperado. Podés volver a intentar o copiar los detalles para reportarlo.";

    private readonly DownloadService _downloadService;
    private readonly NotificationService _notificationService;
    private readonly HistoryService _historyService;
    private readonly SettingsService _settings;
    private readonly Dictionary<DownloadItemViewModel, Task> _running = [];

    public DownloadQueueViewModel(
        DownloadService downloadService,
        NotificationService notificationService,
        HistoryService historyService,
        SettingsService settings)
    {
        _downloadService = downloadService;
        _notificationService = notificationService;
        _historyService = historyService;
        _settings = settings;

        // Si se sube el limite de descargas simultaneas, arrancan las que esperaban
        settings.Changed += (_, _) => StartNext();
    }

    /// <summary>
    /// Pide a la vista que anuncie un resultado a los lectores de pantalla.
    /// </summary>
    public event EventHandler<string>? AnnouncementRequested;

    /// <summary>
    /// Se dispara cuando una descarga termina bien, con la ruta del archivo.
    /// </summary>
    public event EventHandler<string>? ItemCompleted;

    /// <summary>
    /// Todas las descargas de la sesion, de la mas nueva a la mas vieja.
    /// </summary>
    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

    /// <summary>
    /// Las que todavia necesitan atencion (en cola, descargando, en pausa o con error). Las muestra la pagina Descargar.
    /// </summary>
    public ObservableCollection<DownloadItemViewModel> ActiveItems { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool IsEmpty => !HasItems;

    public bool HasActiveItems => ActiveItems.Count > 0;

    public bool HasRunningDownloads => Items.Any(item => item.Status == DownloadStatus.Downloading);

    /// <summary>
    /// Descargas que se perderian al cerrar la app.
    /// </summary>
    public int PendingCount => Items.Count(item => item.Status is DownloadStatus.Queued or DownloadStatus.Downloading or DownloadStatus.Paused);

    public string Summary
    {
        get
        {
            var parts = new List<string>();
            void Add(DownloadStatus status, string singular, string plural)
            {
                var count = Items.Count(item => item.Status == status);
                if (count > 0) parts.Add($"{count} {(count == 1 ? singular : plural)}");
            }

            Add(DownloadStatus.Downloading, "descargando", "descargando");
            Add(DownloadStatus.Queued, "en cola", "en cola");
            Add(DownloadStatus.Paused, "en pausa", "en pausa");
            Add(DownloadStatus.Completed, "completa", "completas");
            Add(DownloadStatus.Failed, "con error", "con error");
            Add(DownloadStatus.Canceled, "cancelada", "canceladas");
            return parts.Count > 0 ? string.Join(" · ", parts) : "No hay descargas";
        }
    }

    public DownloadItemViewModel Enqueue(Uri url, DownloadOptions options, string outputDirectory, VideoInfo? preview)
    {
        var item = new DownloadItemViewModel(this, url, options, outputDirectory, preview);
        Items.Insert(0, item);
        item.Requeue(moveToBack: false);
        StartNext();
        return item;
    }

    private bool CanPauseAll() => Items.Any(item => item.CanPause);

    [RelayCommand(CanExecute = nameof(CanPauseAll))]
    private void PauseAll()
    {
        foreach (var item in Items.Where(item => item.CanPause).ToList()) Pause(item);
    }

    private bool CanResumeAll() => Items.Any(item => item.CanResume);

    [RelayCommand(CanExecute = nameof(CanResumeAll))]
    private void ResumeAll()
    {
        foreach (var item in Items.Where(item => item.CanResume).OrderBy(item => item.EnqueuedAt).ToList()) item.Requeue(moveToBack: false);
        StartNext();
    }

    private bool CanCancelAll() => Items.Any(item => item.CanCancel);

    [RelayCommand(CanExecute = nameof(CanCancelAll))]
    private void CancelAll()
    {
        foreach (var item in Items.Where(item => item.CanCancel).ToList()) Cancel(item);
    }

    private bool CanRemoveFinished() => Items.Any(item => item.Status is DownloadStatus.Completed or DownloadStatus.Canceled or DownloadStatus.Failed);

    [RelayCommand(CanExecute = nameof(CanRemoveFinished))]
    private void RemoveFinished()
    {
        foreach (var item in Items.Where(item => item.Status is DownloadStatus.Completed or DownloadStatus.Canceled or DownloadStatus.Failed).ToList())
        {
            Items.Remove(item);
        }

        OnQueueChanged();
    }

    /// <summary>
    /// Cancela todo lo pendiente y espera a que cada descarga termine de limpiar sus archivos parciales.
    /// </summary>
    public async Task CancelAllAndWaitAsync()
    {
        CancelAll();
        await Task.WhenAll(_running.Values.ToList());

        // Las pausadas no tienen proceso, pero si archivos parciales
        foreach (var item in Items.Where(item => item.WorkDirectory is not null).ToList())
        {
            await DiscardWorkDirectoryAsync(item);
        }
    }

    internal void Pause(DownloadItemViewModel item)
    {
        if (item.Status == DownloadStatus.Downloading)
        {
            item.PauseRequested = true;
            item.Cancellation?.Cancel();
        }
        else if (item.Status == DownloadStatus.Queued)
        {
            item.MarkPaused();
            OnQueueChanged();
        }
    }

    internal void Resume(DownloadItemViewModel item)
    {
        if (item.Status != DownloadStatus.Paused) return;
        item.Requeue(moveToBack: false);
        StartNext();
    }

    internal async void Cancel(DownloadItemViewModel item)
    {
        if (item.Status == DownloadStatus.Downloading)
        {
            item.PauseRequested = false;
            item.Cancellation?.Cancel();
            return;
        }

        if (item.Status is DownloadStatus.Queued or DownloadStatus.Paused)
        {
            item.MarkCanceled();
            OnQueueChanged();
            await DiscardWorkDirectoryAsync(item);
        }
    }

    internal void Retry(DownloadItemViewModel item)
    {
        if (!item.CanRetry) return;
        item.Requeue(moveToBack: true);
        StartNext();
    }

    internal async void Remove(DownloadItemViewModel item)
    {
        if (item.Status == DownloadStatus.Downloading)
        {
            // Se quita al terminar de cancelar, cuando el proceso solto los archivos
            item.RemoveRequested = true;
            item.Cancellation?.Cancel();
            return;
        }

        Items.Remove(item);
        OnQueueChanged();
        await DiscardWorkDirectoryAsync(item);
    }

    internal void OpenFile(DownloadItemViewModel item)
    {
        // Si no se puede abrir (por ejemplo, sin programa asociado), al menos se muestra en su carpeta
        if (item.FilePath is not null && !ShellHelper.TryOpenFile(item.FilePath, out _))
        {
            ShellHelper.TryShowInFolder(item.FilePath, out _);
        }
    }

    internal void ShowInFolder(DownloadItemViewModel item)
    {
        if (item.FilePath is not null) ShellHelper.TryShowInFolder(item.FilePath, out _);
    }

    internal void CopyText(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch (Exception)
        {
            // Si el portapapeles esta ocupado no hay nada mas que hacer
        }
    }

    private void StartNext()
    {
        var limit = _settings.MaxConcurrentDownloads;
        while (Items.Count(item => item.Status == DownloadStatus.Downloading) < limit)
        {
            var next = Items.Where(item => item.Status == DownloadStatus.Queued).OrderBy(item => item.EnqueuedAt).FirstOrDefault();
            if (next is null) break;
            _running[next] = RunAsync(next);
        }

        OnQueueChanged();
    }

    private async Task RunAsync(DownloadItemViewModel item)
    {
        item.BeginDownload();
        item.Cancellation = new CancellationTokenSource();
        OnQueueChanged();

        if (!App.IsMainWindowActive)
        {
            _notificationService.Show("Descarga iniciada", item.Title, item.OutputDirectory);
        }

        try
        {
            item.WorkDirectory ??= DownloadService.CreateWorkDirectory(item.OutputDirectory);

            // Progress<T> devuelve los reportes al hilo de la interfaz
            var progress = new Progress<DownloadProgress>(item.ApplyProgress);
            var info = new Progress<VideoInfo>(item.ApplyInfo);
            var result = await _downloadService.DownloadAsync(
                item.Url, item.OutputDirectory, item.WorkDirectory, item.Options, progress, info, item.Cancellation.Token);

            await DiscardWorkDirectoryAsync(item);

            if (result.Success && result.FilePath is not null)
            {
                item.Complete(result.FilePath);
                AddToHistory(item, result.FilePath);
                _notificationService.Show("Descarga completa", Path.GetFileName(result.FilePath), item.OutputDirectory);
                AnnouncementRequested?.Invoke(this, $"Descarga completa: {item.Title}");
                ItemCompleted?.Invoke(this, result.FilePath);
            }
            else
            {
                ReportFailure(item, result.ErrorMessage ?? UnexpectedErrorMessage, result.ErrorDetails);
            }
        }
        catch (OperationCanceledException)
        {
            if (item.PauseRequested && !item.RemoveRequested)
            {
                // Se conserva la carpeta de trabajo: yt-dlp retoma desde los archivos parciales
                item.MarkPaused();
            }
            else
            {
                await DiscardWorkDirectoryAsync(item);
                item.MarkCanceled();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            await DiscardWorkDirectoryAsync(item);
            ReportFailure(item, "No se puede escribir en la carpeta de destino. Revisá los permisos y volvé a intentar.", ex.Message);
        }
        catch (IOException ex)
        {
            await DiscardWorkDirectoryAsync(item);
            ReportFailure(item, "La carpeta de destino no está disponible. Comprobá que la unidad esté conectada y tenga espacio.", ex.Message);
        }
        catch (Exception ex)
        {
            await DiscardWorkDirectoryAsync(item);
            ReportFailure(item, UnexpectedErrorMessage, ex.Message);
        }
        finally
        {
            item.Cancellation?.Dispose();
            item.Cancellation = null;
            _running.Remove(item);
            if (item.RemoveRequested) Items.Remove(item);
            StartNext();
        }
    }

    private void ReportFailure(DownloadItemViewModel item, string message, string? details)
    {
        item.Fail(message, details);
        if (!App.IsMainWindowActive) _notificationService.Show("No se pudo descargar", item.Title, item.OutputDirectory);
        AnnouncementRequested?.Invoke(this, $"No se pudo descargar {item.Title}: {message}");
    }

    private void AddToHistory(DownloadItemViewModel item, string filePath)
    {
        long? size = null;
        try { size = new FileInfo(filePath).Length; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        _historyService.Add(new HistoryEntry(
            Guid.NewGuid(), item.Url.AbsoluteUri, item.Title, item.Channel, item.ThumbnailUrl, filePath, item.Options, size, DateTimeOffset.Now));
    }

    private static async Task DiscardWorkDirectoryAsync(DownloadItemViewModel item)
    {
        if (item.WorkDirectory is null) return;
        var workDirectory = item.WorkDirectory;
        item.WorkDirectory = null;
        await DownloadService.DeleteWorkDirectoryAsync(workDirectory);
    }

    private void OnQueueChanged()
    {
        SyncActiveItems();
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasActiveItems));
        OnPropertyChanged(nameof(HasRunningDownloads));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(Summary));
        PauseAllCommand.NotifyCanExecuteChanged();
        ResumeAllCommand.NotifyCanExecuteChanged();
        CancelAllCommand.NotifyCanExecuteChanged();
        RemoveFinishedCommand.NotifyCanExecuteChanged();
    }

    // Mantiene ActiveItems en el mismo orden que Items, solo con las que necesitan atencion
    private void SyncActiveItems()
    {
        var desired = Items.Where(item => item.Status is not (DownloadStatus.Completed or DownloadStatus.Canceled)).ToList();

        foreach (var item in ActiveItems.Except(desired).ToList()) ActiveItems.Remove(item);

        for (var index = 0; index < desired.Count; index++)
        {
            var item = desired[index];
            var current = ActiveItems.IndexOf(item);
            if (current == index) continue;
            if (current >= 0) ActiveItems.Move(current, index);
            else ActiveItems.Insert(index, item);
        }
    }
}
