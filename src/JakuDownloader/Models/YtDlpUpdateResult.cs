namespace JakuDownloader.Models;

/// <summary>
/// Resultado de "yt-dlp -U". Si <see cref="ErrorMessage"/> no es null, la actualizacion fallo.
/// </summary>
public sealed record YtDlpUpdateResult(bool Updated, string? Version, string? ErrorMessage, string? ErrorDetails);
