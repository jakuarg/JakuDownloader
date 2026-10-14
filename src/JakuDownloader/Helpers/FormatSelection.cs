using JakuDownloader.Models;

namespace JakuDownloader.Helpers;

/// <summary>
/// Traduce las opciones de descarga a los argumentos de yt-dlp: formato, subtitulos, capitulos y metadatos.
/// </summary>
public static class FormatSelection
{
    public static IEnumerable<string> BuildArguments(DownloadOptions options)
        => (options.Kind == MediaKind.Audio ? AudioArguments(options) : VideoArguments(options))
            .Concat(SubtitleArguments(options))
            .Concat(EmbedArguments(options))
            .Concat(SponsorBlockArguments(options));

    private static IEnumerable<string> SponsorBlockArguments(DownloadOptions options)
    {
        if (options.SponsorBlock == SponsorBlockMode.Off || string.IsNullOrWhiteSpace(options.SponsorBlockCategories)) yield break;

        // Marcar agrega los capitulos aunque "Incrustar capitulos" este apagado: es lo que se pidio
        yield return options.SponsorBlock == SponsorBlockMode.Remove ? "--sponsorblock-remove" : "--sponsorblock-mark";
        yield return options.SponsorBlockCategories;
    }

    private static IEnumerable<string> SubtitleArguments(DownloadOptions options)
    {
        if (!options.WantsSubtitles) yield break;

        if (options.EmbedSubtitles)
        {
            // Sin --write-subs: asi yt-dlp borra los archivos de subtitulos despues de incrustarlos.
            // WebM solo admite WebVTT; mp4 y mkv convierten lo que haga falta
            yield return "--embed-subs";
            yield return "--sub-format";
            yield return "vtt/srt/best";
        }
        else
        {
            yield return "--write-subs";
            yield return "--convert-subs";
            yield return "srt";
        }

        if (options.AutomaticSubtitles) yield return "--write-auto-subs";

        yield return "--sub-langs";
        yield return SubtitleLanguages.Normalize(options.SubtitleLanguages);

        // Un subtitulo que falla (por ejemplo, YouTube limita los pedidos) no tiene que tirar abajo el video
        yield return "--ignore-errors";
    }

    private static IEnumerable<string> EmbedArguments(DownloadOptions options)
    {
        if (options.EmbedChapters) yield return "--embed-chapters";

        if (options.EmbedMetadata)
        {
            yield return "--embed-metadata";
            if (options.SupportsThumbnail)
            {
                yield return "--embed-thumbnail";

                // Las miniaturas de YouTube suelen venir en WebP, que mp4 y mp3 no aceptan como portada
                yield return "--convert-thumbnails";
                yield return "jpg";
            }
        }
    }

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
