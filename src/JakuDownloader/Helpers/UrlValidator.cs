using System.Diagnostics.CodeAnalysis;

namespace JakuDownloader.Helpers;

public static class UrlValidator
{
    public static bool TryParse(string? text, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var candidate = text.Trim();
        // Permite pegar links sin esquema, como "youtu.be/abc"
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            && parsed.Host.Contains('.'))
        {
            uri = parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// True si el link es de una lista de reproduccion o de un canal de YouTube, no de un video.
    /// </summary>
    /// <remarks>
    /// Se detecta antes de llamar a yt-dlp: con estos links ignora --no-playlist y recorre todos los videos.
    /// Un video dentro de una lista (watch?v=...&amp;list=...) cuenta como video.
    /// </remarks>
    public static bool IsCollection(Uri uri)
    {
        if (!uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)) return false;

        string[] collectionPaths = ["/playlist", "/@", "/channel/", "/c/", "/user/"];
        if (collectionPaths.Any(prefix => uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var query = uri.Query;
        return query.Contains("list=", StringComparison.Ordinal) && !query.Contains("v=", StringComparison.Ordinal);
    }
}
