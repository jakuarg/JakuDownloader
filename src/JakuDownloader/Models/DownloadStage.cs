namespace JakuDownloader.Models;

public enum DownloadStage
{
    FetchingInfo,
    DownloadingVideo,
    DownloadingAudio,
    Downloading,
    Merging,
    Processing,
}
