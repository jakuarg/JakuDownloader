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
}
