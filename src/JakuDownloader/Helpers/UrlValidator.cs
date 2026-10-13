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

    /// <summary>
    /// True si es un video abierto desde una lista (watch?v=...&amp;list=...): se puede bajar solo o con toda la lista.
    /// </summary>
    public static bool IsVideoInPlaylist(Uri uri)
    {
        if (GetQueryValue(uri, "list") is null || IsCollection(uri)) return false;

        // youtube.com/watch?v=...&list=... o el link corto youtu.be/ID?list=...
        return uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)
            ? GetQueryValue(uri, "v") is not null
            : uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Trim('/').Length > 0;
    }

    /// <summary>
    /// Link para listar los videos: la lista de un video abierto desde una lista, o la pestana "Videos"
    /// de un canal (la raiz del canal devuelve sus pestanas, no sus videos).
    /// </summary>
    public static Uri ToPlaylistUrl(Uri uri)
    {
        if (IsVideoInPlaylist(uri))
        {
            return new Uri($"https://www.youtube.com/playlist?list={Uri.EscapeDataString(GetQueryValue(uri, "list")!)}");
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var isChannelRoot = segments.Length switch
        {
            1 => segments[0].StartsWith('@'),
            2 => segments[0] is "channel" or "c" or "user",
            _ => false,
        };

        return isChannelRoot ? new Uri($"https://www.youtube.com/{string.Join('/', segments)}/videos") : uri;
    }

    private static string? GetQueryValue(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == name && parts[1].Length > 0) return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }
}
