using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Services;

namespace JakuDownloader.ViewModels;

public sealed partial class MainViewModel(
    DownloadService downloadService,
    NotificationService notificationService,
    ToolLocator toolLocator) : ObservableObject
{
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public bool IsIdle => !IsDownloading;

    private bool CanDownload() => !IsDownloading && !string.IsNullOrWhiteSpace(Url);

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (!UrlValidator.TryParse(Url, out var uri))
        {
            StatusMessage = "El link no es válido.";
            return;
        }

        var missingTools = toolLocator.GetMissingTools();
        if (missingTools.Count > 0)
        {
            StatusMessage = $"Faltan herramientas en la carpeta tools: {string.Join(", ", missingTools)}";
            return;
        }

        var outputDirectory = AppPaths.DownloadDirectory;
        Directory.CreateDirectory(outputDirectory);
        notificationService.Show("Descarga iniciada", $"El archivo se va a guardar en {outputDirectory}");

        IsDownloading = true;
        StatusMessage = "Descargando...";
        _cancellation = new CancellationTokenSource();

        try
        {
            var result = await downloadService.DownloadAsync(uri, outputDirectory, _cancellation.Token);
            if (result.Success)
            {
                StatusMessage = $"Listo: {Path.GetFileName(result.FilePath)}";
                Url = string.Empty;
            }
            else
            {
                StatusMessage = $"Error: {result.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Descarga cancelada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsDownloading = false;
        }
    }

    public void CancelDownload() => _cancellation?.Cancel();
}
