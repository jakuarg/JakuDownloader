namespace JakuDownloader.Models;

/// <summary>
/// Configuracion guardada. Null significa "usar el valor por defecto" (o "no usar", en las opciones de red).
/// </summary>
public sealed record AppSettings(
    string? DownloadDirectory = null,
    string? FileNameTemplate = null,
    int? MaxConcurrentDownloads = null,
    string? CookiesBrowser = null,
    string? CookiesFile = null,
    string? Proxy = null,
    string? RateLimit = null);
