namespace JakuDownloader.Models;

/// <summary>
/// Una descarga terminada. Guarda las opciones para poder repetirla igual.
/// </summary>
public sealed record HistoryEntry(
    Guid Id,
    string Url,
    string Title,
    string? Channel,
    string? ThumbnailUrl,
    string FilePath,
    DownloadOptions Options,
    long? SizeBytes,
    DateTimeOffset CompletedAt);
