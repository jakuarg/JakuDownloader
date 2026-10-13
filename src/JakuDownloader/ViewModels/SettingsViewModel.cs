using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Services;

namespace JakuDownloader.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly FolderPickerService _folderPicker;

    public SettingsViewModel(SettingsService settings, FolderPickerService folderPicker, ToolsViewModel tools)
    {
        _settings = settings;
        _folderPicker = folderPicker;
        Tools = tools;
        DownloadDirectory = settings.DownloadDirectory;
        TemplateText = settings.FileNameTemplate;
    }

    public ToolsViewModel Tools { get; }

    public string AppVersion => AppIdentity.DisplayVersion;

    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    [ObservableProperty]
    public partial string ErrorTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    // Carpeta de destino

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomDirectory))]
    public partial string DownloadDirectory { get; set; }

    public bool IsCustomDirectory => _settings.Current.DownloadDirectory is not null;

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        IsErrorOpen = false;
        if (!ShellHelper.TryOpenFolder(DownloadDirectory, out var error)) ShowError("No se pudo abrir la carpeta", error);
    }

    [RelayCommand]
    private async Task ChangeDownloadFolderAsync()
    {
        IsErrorOpen = false;
        string? folder;
        try
        {
            folder = await _folderPicker.PickFolderAsync();
        }
        catch (Exception)
        {
            ShowError("No se pudo abrir el selector de carpetas", "Volvé a intentar.");
            return;
        }

        if (folder is null) return;

        if (!AppPaths.IsWritable(folder))
        {
            ShowError("No se puede usar esa carpeta", $"No hay permiso para guardar archivos en {folder}. Elegí otra.");
            return;
        }

        // Elegir la carpeta por defecto equivale a no tener una personalizada
        var isDefault = string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), Path.GetFullPath(AppPaths.DefaultDownloadDirectory).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
        SaveDownloadDirectory(isDefault ? null : folder);
    }

    [RelayCommand]
    private void ResetDownloadFolder() => SaveDownloadDirectory(null);

    private void SaveDownloadDirectory(string? folder)
    {
        if (!TrySave(current => current with { DownloadDirectory = folder })) return;
        DownloadDirectory = _settings.DownloadDirectory;
        OnPropertyChanged(nameof(IsCustomDirectory));
    }

    // Plantilla del nombre de archivo

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemplateError))]
    [NotifyPropertyChangedFor(nameof(IsTemplateValid))]
    [NotifyPropertyChangedFor(nameof(IsTemplateInvalid))]
    [NotifyPropertyChangedFor(nameof(TemplateExample))]
    [NotifyCanExecuteChangedFor(nameof(SaveTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetTemplateCommand))]
    public partial string TemplateText { get; set; }

    public string TemplateError => FileNameTemplate.Validate(TemplateText) ?? string.Empty;

    public bool IsTemplateValid => FileNameTemplate.Validate(TemplateText) is null;

    public bool IsTemplateInvalid => !IsTemplateValid;

    public string TemplateExample => IsTemplateValid ? $"Ejemplo: {FileNameTemplate.Preview(TemplateText)}" : string.Empty;

    private bool CanSaveTemplate() => IsTemplateValid && TemplateText != _settings.FileNameTemplate;

    [RelayCommand(CanExecute = nameof(CanSaveTemplate))]
    private void SaveTemplate()
    {
        IsErrorOpen = false;
        var template = TemplateText == FileNameTemplate.Default ? null : TemplateText;
        if (TrySave(current => current with { FileNameTemplate = template }))
        {
            SaveTemplateCommand.NotifyCanExecuteChanged();
            ResetTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanResetTemplate() => TemplateText != FileNameTemplate.Default || _settings.Current.FileNameTemplate is not null;

    [RelayCommand(CanExecute = nameof(CanResetTemplate))]
    private void ResetTemplate()
    {
        TemplateText = FileNameTemplate.Default;
        if (TrySave(current => current with { FileNameTemplate = null }))
        {
            SaveTemplateCommand.NotifyCanExecuteChanged();
            ResetTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool TrySave(Func<Models.AppSettings, Models.AppSettings> change)
    {
        try
        {
            _settings.Update(change);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("No se pudo guardar la configuración", $"No se pudo escribir en {AppPaths.SettingsFile}.");
            return false;
        }
    }

    private void ShowError(string title, string message)
    {
        ErrorTitle = title;
        ErrorMessage = message;
        IsErrorOpen = true;
    }
}
