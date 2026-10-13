using Windows.Storage.Pickers;

namespace JakuDownloader.Services;

/// <summary>
/// Dialogo de Windows para elegir una carpeta.
/// </summary>
public sealed class FolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");

        // En apps sin empaquetar el dialogo necesita saber cual es su ventana
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
