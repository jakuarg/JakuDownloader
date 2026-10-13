namespace JakuDownloader.Models;

public enum DownloadStatus
{
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed,
    Canceled,
}
