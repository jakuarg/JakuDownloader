namespace JakuDownloader.Models;

/// <summary>
/// Datos del video para la vista previa.
/// </summary>
public sealed record VideoInfo(
    string Id,
    string Title,
    string? Channel,
    int? DurationSeconds,
    string? ThumbnailUrl,
    bool IsLive,
    bool IsUpcoming);
