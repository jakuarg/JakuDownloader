namespace JakuDownloader.Models;

/// <summary>
/// Lo que el usuario eligio para una descarga. Los campos de video se ignoran en solo audio y viceversa.
/// </summary>
public sealed record DownloadOptions(
    MediaKind Kind,
    VideoContainer Container = VideoContainer.Mp4,
    AudioFormat AudioFormat = AudioFormat.Mp3,
    int? MaxHeight = null,
    VideoCodec VideoCodec = VideoCodec.Auto,
    AudioCodec AudioCodec = AudioCodec.Auto)
{
    public string Extension => Kind == MediaKind.Audio
        ? AudioFormat.ToString().ToLowerInvariant()
        : Container.ToString().ToLowerInvariant();

    /// <summary>
    /// Descripcion corta para listas, por ejemplo "MP4 · hasta 1080p" o "MP3".
    /// </summary>
    public string Label => Kind == MediaKind.Audio
        ? Extension.ToUpperInvariant()
        : MaxHeight is int height ? $"{Extension.ToUpperInvariant()} · hasta {height}p" : Extension.ToUpperInvariant();

    /// <summary>
    /// M4A y Opus se guardan tal como vienen de YouTube; el resto se convierte.
    /// </summary>
    public bool ConvertsAudio => Kind == MediaKind.Audio && AudioFormat is not (AudioFormat.M4a or AudioFormat.Opus);
}
