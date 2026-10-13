using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;
using JakuDownloader.Services;

namespace JakuDownloader.ViewModels;

public sealed partial class SettingsViewModel(ToolLocator toolLocator) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public string DownloadDirectory { get; } = AppPaths.DownloadDirectory;

    public string FileNameTemplate => DownloadService.OutputTemplate;

    public string AppVersion => AppIdentity.DisplayVersion;

    public IReadOnlyList<ToolInfo> Tools { get; } = toolLocator.GetToolInfos();

    [RelayCommand]
    private void OpenDownloadFolder() => OpenFolder(DownloadDirectory);

    [RelayCommand]
    private void OpenToolsFolder() => OpenFolder(toolLocator.ToolsDirectory);

    private void OpenFolder(string folder)
    {
        IsErrorOpen = false;
        if (!ShellHelper.TryOpenFolder(folder, out var error))
        {
            ErrorMessage = error;
            IsErrorOpen = true;
        }
    }
}
