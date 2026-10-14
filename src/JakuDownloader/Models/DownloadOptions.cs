namespace JakuDownloader.Models;

/// <summary>
/// Lo que el usuario eligio para una descarga. Los campos de video se ignoran en solo audio y viceversa.
/// </summary>
/// <remarks>
/// Los parametros nuevos van al final y con valor por defecto: asi se siguen leyendo las entradas
/// del historial guardadas con versiones anteriores.
/// </remarks>
public sealed record DownloadOptions(
    MediaKind Kind,
    VideoContainer Container = VideoContainer.Mp4,
    AudioFormat AudioFormat = AudioFormat.Mp3,
    int? MaxHeight = null,
    VideoCodec VideoCodec = VideoCodec.Auto,
    AudioCodec AudioCodec = AudioCodec.Auto,
    bool Subtitles = false,
    string SubtitleLanguages = "es,en",
    bool AutomaticSubtitles = true,
    bool EmbedSubtitles = true,
    bool EmbedChapters = true,
    bool EmbedMetadata = true,
    SponsorBlockMode SponsorBlock = SponsorBlockMode.Off,
    string SponsorBlockCategories = Helpers.SponsorBlockCategories.Default)
{
    /// <summary>
    /// Formatos donde yt-dlp puede incrustar la miniatura. WebM y WAV no la admiten y la descarga fallaria.
    /// </summary>
    public bool SupportsThumbnail => Kind == MediaKind.Audio
        ? AudioFormat is AudioFormat.Mp3 or AudioFormat.M4a or AudioFormat.Opus or AudioFormat.Flac
        : Container is VideoContainer.Mp4 or VideoContainer.Mkv;

    /// <summary>
    /// Subtitulos solo tienen sentido en video.
    /// </summary>
    public bool WantsSubtitles => Kind == MediaKind.Video && Subtitles;

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
