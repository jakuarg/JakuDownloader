namespace JakuDownloader.Models;

/// <summary>
/// Configuracion guardada. Null significa "usar el valor por defecto".
/// </summary>
public sealed record AppSettings(string? DownloadDirectory = null, string? FileNameTemplate = null);
