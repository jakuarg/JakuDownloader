namespace JakuDownloader.Models;

/// <summary>
/// Configuracion guardada. Null significa "usar el valor por defecto" (o "no usar", en las opciones de red).
/// </summary>
/// <remarks>
/// El inicio con Windows no esta aca: vive en el registro, que es lo que Windows lee (ver <see cref="Helpers.StartupRegistration"/>).
/// </remarks>
public sealed record AppSettings(
    string? DownloadDirectory = null,
    string? FileNameTemplate = null,
    int? MaxConcurrentDownloads = null,
    string? CookiesBrowser = null,
    string? CookiesFile = null,
    string? Proxy = null,
    string? RateLimit = null,
    bool? KeepRunningInBackground = null,
    bool BackgroundNoticeShown = false);
