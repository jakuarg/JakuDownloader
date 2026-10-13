using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;
using JakuDownloader.Services;
using Microsoft.UI.Xaml.Controls;

namespace JakuDownloader.ViewModels;

/// <summary>
/// Estado de yt-dlp, FFmpeg y Deno: que falta, su instalacion y la actualizacion de yt-dlp.
/// Lo comparten la pagina Descargar y Configuracion.
/// </summary>
public sealed partial class ToolsViewModel : ObservableObject
{
    private const string DefaultUpdateDescription =
        "YouTube cambia seguido y casi todos los errores raros se arreglan actualizando.";

    private readonly ToolLocator _toolLocator;
    private readonly ToolInstaller _toolInstaller;
    private readonly DownloadViewModel _downloadViewModel;
    private readonly DownloadQueueViewModel _queue;

    public ToolsViewModel(ToolLocator toolLocator, ToolInstaller toolInstaller, DownloadViewModel downloadViewModel, DownloadQueueViewModel queue)
    {
        _toolLocator = toolLocator;
        _toolInstaller = toolInstaller;
        _downloadViewModel = downloadViewModel;
        _queue = queue;
        Refresh();
    }

    [ObservableProperty]
    public partial IReadOnlyList<ToolInfo> Tools { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissingTools))]
    [NotifyPropertyChangedFor(nameof(IsMissingBannerOpen))]
    [NotifyPropertyChangedFor(nameof(MissingToolsMessage))]
    [NotifyPropertyChangedFor(nameof(InstallDescription))]
    [NotifyCanExecuteChangedFor(nameof(InstallMissingCommand))]
    public partial IReadOnlyList<Tool> MissingTools { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissingBannerOpen))]
    [NotifyPropertyChangedFor(nameof(InstallDescription))]
    [NotifyCanExecuteChangedFor(nameof(InstallMissingCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateYtDlpCommand))]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial double InstallProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallDescription))]
    public partial string InstallStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsInstallResultOpen { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity InstallResultSeverity { get; set; }

    [ObservableProperty]
    public partial string InstallResultTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InstallResultMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallMissingCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateYtDlpCommand))]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial string UpdateDescription { get; set; } = DefaultUpdateDescription;

    public bool HasMissingTools => MissingTools.Count > 0;

    /// <summary>
    /// El aviso de herramientas faltantes se oculta mientras se instalan: en su lugar se ve el progreso.
    /// </summary>
    public bool IsMissingBannerOpen => HasMissingTools && !IsInstalling;

    public string MissingToolsMessage
    {
        get
        {
            if (!HasMissingTools) return string.Empty;
            var names = string.Join(", ", MissingTools.Select(ToolLocator.DisplayName));
            var size = MissingTools.Sum(ToolInstaller.ApproximateSizeMegabytes);
            return $"Para descargar hacen falta {names}. Se bajan una sola vez desde sus páginas oficiales (unos {size} MB).";
        }
    }

    public string InstallDescription => IsInstalling
        ? InstallStatus
        : HasMissingTools
            ? $"Faltan: {string.Join(", ", MissingTools.Select(ToolLocator.DisplayName))}"
            : "Están todas instaladas";

    public string ToolsDirectory => _toolLocator.ToolsDirectory;

    public void Refresh()
    {
        Tools = _toolLocator.GetToolInfos();
        MissingTools = _toolLocator.GetMissing();
        UpdateYtDlpCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ToolsDirectory));
    }

    private bool CanInstall() => HasMissingTools && !IsInstalling && !IsUpdating;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallMissingAsync()
    {
        IsInstallResultOpen = false;
        if (_queue.HasRunningDownloads)
        {
            ShowInstallResult(InfoBarSeverity.Warning, "Hay una descarga en curso", "Esperá a que termine y volvé a intentar.");
            return;
        }

        var tools = MissingTools;
        InstallProgress = 0;
        InstallStatus = "Preparando...";
        IsInstalling = true;

        try
        {
            var progress = new Progress<ToolInstallProgress>(ApplyInstallProgress);
            await _toolInstaller.InstallAsync(tools, progress, CancellationToken.None);
            ShowInstallResult(InfoBarSeverity.Success, "Herramientas listas", "Ya podés descargar.");
        }
        catch (HttpRequestException)
        {
            ShowInstallResult(InfoBarSeverity.Error, "No se pudieron descargar las herramientas",
                "No se pudo conectar con GitHub. Revisá la conexión y volvé a intentar.");
        }
        catch (TaskCanceledException)
        {
            ShowInstallResult(InfoBarSeverity.Error, "No se pudieron descargar las herramientas",
                "La descarga tardó demasiado. Revisá la conexión y volvé a intentar.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowInstallResult(InfoBarSeverity.Error, "No se pudieron instalar las herramientas",
                $"No se pudo escribir en {_toolLocator.InstallDirectory}. Cerrá otras ventanas de Jaku Downloader y volvé a intentar.");
        }
        catch (Exception ex) when (ex is InvalidDataException or PlatformNotSupportedException)
        {
            ShowInstallResult(InfoBarSeverity.Error, "No se pudieron instalar las herramientas", ex.Message);
        }
        catch (Exception ex)
        {
            ShowInstallResult(InfoBarSeverity.Error, "No se pudieron instalar las herramientas", ex.Message);
        }
        finally
        {
            IsInstalling = false;
            Refresh();
            _downloadViewModel.ReloadPreview();
        }
    }

    private bool CanUpdate() => !IsInstalling && !IsUpdating && Tools.Any(tool => tool.Name == "yt-dlp" && tool.IsInstalled);

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateYtDlpAsync()
    {
        // Mientras descarga, yt-dlp.exe esta en uso y no se puede reemplazar
        if (_queue.HasRunningDownloads)
        {
            UpdateDescription = "Esperá a que termine la descarga en curso y volvé a intentar.";
            return;
        }

        IsUpdating = true;
        UpdateDescription = "Buscando actualización...";

        try
        {
            var result = await _toolInstaller.UpdateYtDlpAsync(CancellationToken.None);
            UpdateDescription = result switch
            {
                { ErrorMessage: { } error } => error,
                { Updated: true } => $"Se actualizó a la versión {result.Version}.",
                _ => $"Ya tenés la última versión ({result.Version}).",
            };
        }
        catch (Exception)
        {
            UpdateDescription = "No se pudo actualizar yt-dlp. Revisá la conexión y volvé a intentar.";
        }
        finally
        {
            IsUpdating = false;
            Refresh();
        }
    }

    [RelayCommand]
    private void OpenToolsFolder()
    {
        if (!ShellHelper.TryOpenFolder(ToolsDirectory, out var error))
        {
            ShowInstallResult(InfoBarSeverity.Warning, "No se pudo abrir la carpeta", error);
        }
    }

    private void ApplyInstallProgress(ToolInstallProgress progress)
    {
        InstallProgress = progress.OverallPercent;
        var name = ToolLocator.DisplayName(progress.Tool);
        var position = progress.Count > 1 ? $" ({progress.Index + 1} de {progress.Count})" : string.Empty;
        InstallStatus = progress.Stage == ToolInstallStage.Downloading
            ? $"Descargando {name}{position}" + (progress.Fraction is double fraction ? $" · {fraction * 100:0} %" : string.Empty)
            : $"Instalando {name}{position}...";
    }

    private void ShowInstallResult(InfoBarSeverity severity, string title, string message)
    {
        InstallResultSeverity = severity;
        InstallResultTitle = title;
        InstallResultMessage = message;
        IsInstallResultOpen = true;
    }
}
