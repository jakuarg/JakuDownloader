namespace JakuDownloader.Helpers;

public static class AppPaths
{
    public static string ToolsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "tools");

    public static string IconIcoPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

    public static string DownloadDirectory => Path.Combine(NativeMethods.GetDownloadsFolder(), "Jaku Downloader");
}
