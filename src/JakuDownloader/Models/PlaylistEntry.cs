namespace JakuDownloader.Models;

public sealed record PlaylistEntry(string Id, string Title, string Url, int? DurationSeconds, string? ThumbnailUrl);
