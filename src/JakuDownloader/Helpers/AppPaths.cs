namespace JakuDownloader.Helpers;

public static class AppPaths
{
    public static string ToolsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "tools");

    public static string IconIcoPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

    public static string DownloadDirectory => Path.Combine(NativeMethods.GetDownloadsFolder(), "Jaku Downloader");

    // Dentro de la carpeta de destino para que mover el archivo final sea instantaneo (mismo disco)
    public static string GetTempRoot(string outputDirectory) => Path.Combine(outputDirectory, ".jaku-temp");
}
