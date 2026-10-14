using System.Text.Json;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

/// <summary>
/// Lee y guarda la configuracion en %LOCALAPPDATA%\Jaku Downloader\settings.json.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsService()
    {
        Current = Load();
    }

    public event EventHandler? Changed;

    public AppSettings Current { get; private set; }

    public string DownloadDirectory => Current.DownloadDirectory ?? AppPaths.DefaultDownloadDirectory;

    public string FileNameTemplate => Current.FileNameTemplate ?? Helpers.FileNameTemplate.Default;

    public const int DefaultMaxConcurrentDownloads = 3;

    public int MaxConcurrentDownloads => Math.Clamp(Current.MaxConcurrentDownloads ?? DefaultMaxConcurrentDownloads, 1, 10);

    /// <summary>
    /// Cerrar la ventana la oculta y las descargas siguen. Activado por defecto.
    /// </summary>
    public bool KeepRunningInBackground => Current.KeepRunningInBackground ?? true;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFile)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Un archivo roto o ilegible no tiene que impedir que la app arranque
            return new AppSettings();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(AppPaths.UserDataDirectory);

        // Se escribe en un archivo aparte y se reemplaza, asi un corte a mitad no deja el archivo roto
        var temp = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions));
        File.Move(temp, AppPaths.SettingsFile, overwrite: true);
    }
}
