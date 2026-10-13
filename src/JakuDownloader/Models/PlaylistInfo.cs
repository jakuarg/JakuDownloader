namespace JakuDownloader.Models;

/// <summary>
/// Lista de reproduccion o videos de un canal. <see cref="IsTruncated"/> indica que hay mas videos que los cargados.
/// </summary>
public sealed record PlaylistInfo(string Title, string? Channel, IReadOnlyList<PlaylistEntry> Entries, int? TotalCount, bool IsTruncated);
