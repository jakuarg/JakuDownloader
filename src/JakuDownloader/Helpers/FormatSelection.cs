using JakuDownloader.Models;

namespace JakuDownloader.Helpers;

/// <summary>
/// Traduce las opciones de descarga a los argumentos de formato de yt-dlp.
/// </summary>
public static class FormatSelection
{
    public static IEnumerable<string> BuildArguments(DownloadOptions options)
        => options.Kind == MediaKind.Audio ? AudioArguments(options) : VideoArguments(options);

    private static IEnumerable<string> AudioArguments(DownloadOptions options)
    {
        // M4A y Opus: se pide el audio original en ese codec para no recomprimirlo
        yield return "-f";
        yield return options.AudioFormat switch
        {
            AudioFormat.M4a => "ba[ext=m4a]/ba/b",
            AudioFormat.Opus => "ba[acodec=opus]/ba/b",
            _ => "ba/b",
        };

        yield return "-x";
        yield return "--audio-format";
        yield return options.Extension;
        yield return "--audio-quality";
        yield return "0";
    }

    private static IEnumerable<string> VideoArguments(DownloadOptions options)
    {
        yield return "-f";
        yield return VideoFilter(options);

        yield return "-S";
        yield return string.Join(',', SortFields(options));

        yield return "--merge-output-format";
        yield return options.Extension;

        // WebM no admite H.264 ni AAC: si no hay flujos webm, yt-dlp lo deja en otro contenedor en lugar de fallar
        if (options.Container != VideoContainer.Webm)
        {
            yield return "--remux-video";
            yield return options.Extension;
        }
    }

    private static string VideoFilter(DownloadOptions options)
    {
        if (options.Container == VideoContainer.Webm)
        {
            return "bv*[ext=webm]+ba[ext=webm]/b[ext=webm]/bv*+ba/b";
        }

        // Si el video no esta en el codec pedido, se cae al mejor disponible en lugar de fallar
        var codecPattern = options.VideoCodec switch
        {
            VideoCodec.H264 => "^(avc1|h264)",
            VideoCodec.Vp9 => "^vp0?9",
            VideoCodec.Av1 => "^av01",
            _ => null,
        };

        return codecPattern is null ? "bv*+ba/b" : $"bv*[vcodec~='{codecPattern}']+ba/bv*+ba/b";
    }

    private static IEnumerable<string> SortFields(DownloadOptions options)
    {
        yield return options.MaxHeight is int height ? $"res:{height}" : "res";
        yield return "fps";

        // Automatico: a igual resolucion, H.264 + AAC para que el archivo se reproduzca en cualquier lado
        if (options.VideoCodec == VideoCodec.Auto && options.Container != VideoContainer.Webm)
        {
            yield return "vcodec:h264";
        }

        yield return options.AudioCodec == AudioCodec.Opus ? "acodec:opus" : "acodec:aac";
    }
}
