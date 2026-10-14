namespace JakuDownloader.Models;

public enum DownloadStage
{
    FetchingInfo,
    DownloadingVideo,
    DownloadingAudio,
    Downloading,
    Merging,
    ConvertingAudio,
    EmbeddingSubtitles,
    AddingMetadata,
    EmbeddingThumbnail,
    CheckingSponsorBlock,
    ApplyingSponsorBlock,
    Processing,
}
