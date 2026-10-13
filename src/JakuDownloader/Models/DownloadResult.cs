namespace JakuDownloader.Models;

public sealed record DownloadResult(bool Success, string? FilePath, string? ErrorMessage)
{
    public static DownloadResult Completed(string? filePath) => new(true, filePath, null);

    public static DownloadResult Failed(string errorMessage) => new(false, null, errorMessage);
}
