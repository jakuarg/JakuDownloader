namespace JakuDownloader.Models;

/// <summary>
/// Estado de la parte que se esta descargando. Video y audio se bajan por separado,
/// asi que el porcentaje vuelve a cero al pasar de una parte a la otra.
/// </summary>
public sealed record DownloadProgress(
    DownloadStage Stage,
    long? DownloadedBytes = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    int? SecondsRemaining = null)
{
    public double? Percent => DownloadedBytes is long done && TotalBytes is long total && total > 0
        ? Math.Clamp(done * 100.0 / total, 0, 100)
        : null;
}
