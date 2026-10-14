using System.Globalization;
using JakuDownloader.Models;

namespace JakuDownloader.Helpers;

/// <summary>
/// Opciones de red de yt-dlp: cookies, proxy y limite de velocidad.
/// </summary>
public static class NetworkOptions
{
    /// <summary>
    /// Navegadores de los que yt-dlp sabe leer cookies: identificador de yt-dlp y nombre para mostrar.
    /// </summary>
    public static IReadOnlyList<(string Id, string Label)> Browsers { get; } =
    [
        ("firefox", "Firefox"),
        ("chrome", "Chrome"),
        ("edge", "Edge"),
        ("brave", "Brave"),
        ("opera", "Opera"),
        ("vivaldi", "Vivaldi"),
        ("chromium", "Chromium"),
    ];

    private static readonly string[] ProxySchemes = ["http", "https", "socks4", "socks4a", "socks5", "socks5h"];

    /// <summary>
    /// Argumentos para todas las llamadas a yt-dlp (vista previa, listas y descargas).
    /// </summary>
    public static IEnumerable<string> CommonArguments(AppSettings settings)
    {
        // El archivo tiene prioridad: si se eligio uno, es porque el navegador no alcanzaba
        if (!string.IsNullOrWhiteSpace(settings.CookiesFile))
        {
            yield return "--cookies";
            yield return settings.CookiesFile;
        }
        else if (!string.IsNullOrWhiteSpace(settings.CookiesBrowser))
        {
            yield return "--cookies-from-browser";
            yield return settings.CookiesBrowser;
        }

        if (!string.IsNullOrWhiteSpace(settings.Proxy))
        {
            yield return "--proxy";
            yield return settings.Proxy;
        }
    }

    /// <summary>
    /// Argumentos solo para descargas: el limite de velocidad no tiene sentido al pedir informacion.
    /// </summary>
    public static IEnumerable<string> DownloadArguments(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.RateLimit)) yield break;
        yield return "--limit-rate";
        yield return settings.RateLimit;
    }

    /// <summary>
    /// Devuelve null si el proxy es valido, o el motivo para mostrar si no lo es.
    /// </summary>
    public static string? ValidateProxy(string text)
    {
        var value = text.Trim();
        if (value.Length == 0) return "Escribí la dirección del proxy.";

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !ProxySchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return "Tiene que empezar con http://, https:// o socks5://, por ejemplo socks5://127.0.0.1:1080";
        }

        if (uri.Host.Length == 0 || uri.IsDefaultPort && !value.Contains($":{uri.Port}", StringComparison.Ordinal))
        {
            return "Falta el puerto, por ejemplo socks5://127.0.0.1:1080";
        }

        return null;
    }

    /// <summary>
    /// Valor para --limit-rate: "500K" o "5M". Null si el valor no es valido.
    /// </summary>
    public static string? FormatRateLimit(double value, bool megabytes)
    {
        if (double.IsNaN(value) || value <= 0) return null;
        var rounded = Math.Round(value, 1);
        return rounded.ToString("0.#", CultureInfo.InvariantCulture) + (megabytes ? "M" : "K");
    }

    /// <summary>
    /// Separa "5M" o "500K" en numero y unidad. Por defecto, 5 MB/s.
    /// </summary>
    public static (double Value, bool Megabytes) ParseRateLimit(string? rateLimit)
    {
        if (rateLimit is { Length: >= 2 }
            && double.TryParse(rateLimit[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return (value, char.ToUpperInvariant(rateLimit[^1]) == 'M');
        }

        return (5, true);
    }

    /// <summary>
    /// Mira si el archivo parece un cookies.txt de Netscape, que es el unico formato que acepta yt-dlp.
    /// </summary>
    public static bool LooksLikeCookiesFile(string path)
    {
        try
        {
            var first = File.ReadLines(path).FirstOrDefault(line => line.Trim().Length > 0) ?? string.Empty;
            return first.Contains("Netscape HTTP Cookie File", StringComparison.OrdinalIgnoreCase)
                || first.Contains("HTTP Cookie File", StringComparison.OrdinalIgnoreCase)
                || first.Split('\t').Length >= 7;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
