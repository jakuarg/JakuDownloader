namespace JakuDownloader.Helpers;

public static class AppPaths
{
    /// <summary>
    /// Herramientas junto al ejecutable: es donde vienen en la version portable.
    /// </summary>
    public static string ToolsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "tools");

    /// <summary>
    /// Datos del usuario (configuracion y herramientas si la carpeta de la app no admite escritura).
    /// </summary>
    public static string UserDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jaku Downloader");

    public static string UserToolsDirectory { get; } = Path.Combine(UserDataDirectory, "tools");

    public static string SettingsFile { get; } = Path.Combine(UserDataDirectory, "settings.json");

    public static string IconIcoPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

    public static string DefaultDownloadDirectory => Path.Combine(NativeMethods.GetDownloadsFolder(), "Jaku Downloader");

    // Dentro de la carpeta de destino para que mover el archivo final sea instantaneo (mismo disco)
    public static string GetTempRoot(string outputDirectory) => Path.Combine(outputDirectory, ".jaku-temp");

    /// <summary>
    /// True si se pueden crear archivos en la carpeta (la crea si no existe).
    /// </summary>
    public static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".jaku-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
