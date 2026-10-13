using System.Globalization;
using JakuDownloader.Models;

namespace JakuDownloader.Helpers;

/// <summary>
/// Define las lineas que yt-dlp imprime para la app y las interpreta.
/// </summary>
public static class YtDlpOutput
{
    private const string DownloadPrefix = "[jaku-dl] ";
    private const string PostprocessPrefix = "[jaku-pp] ";
    private const string FilePrefix = "[jaku-file] ";

    public const string DownloadTemplate = "download:" + DownloadPrefix
        + "%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s"
        + "|%(progress.speed)s|%(progress.eta)s|%(info.vcodec)s|%(info.acodec)s";

    public const string PostprocessTemplate = "postprocess:" + PostprocessPrefix
        + "%(progress.status)s|%(progress.postprocessor)s";

    public const string FilePathTemplate = "after_move:" + FilePrefix + "%(filepath)s";

    public static bool TryParseFilePath(string line, out string filePath)
    {
        filePath = line.StartsWith(FilePrefix, StringComparison.Ordinal) ? line[FilePrefix.Length..].Trim() : string.Empty;
        return filePath.Length > 0;
    }

    public static bool TryParseProgress(string line, out DownloadProgress progress)
    {
        progress = null!;

        if (line.StartsWith(DownloadPrefix, StringComparison.Ordinal))
        {
            var fields = line[DownloadPrefix.Length..].Split('|');
            if (fields.Length < 8) return false;

            var hasVideo = fields[6] is not ("none" or "NA");
            var hasAudio = fields[7] is not ("none" or "NA");
            var stage = (hasVideo, hasAudio) switch
            {
                (true, false) => DownloadStage.DownloadingVideo,
                (false, true) => DownloadStage.DownloadingAudio,
                _ => DownloadStage.Downloading,
            };

            progress = new DownloadProgress(
                stage,
                ParseLong(fields[1]),
                ParseLong(fields[2]) ?? ParseLong(fields[3]),
                ParseDouble(fields[4]),
                (int?)ParseLong(fields[5]));
            return true;
        }

        if (line.StartsWith(PostprocessPrefix, StringComparison.Ordinal))
        {
            var fields = line[PostprocessPrefix.Length..].Split('|');
            if (fields.Length < 2 || fields[0] != "started") return false;

            // MoveFiles es instantaneo, no vale la pena mostrarlo
            if (fields[1] == "MoveFiles") return false;

            var stage = fields[1] switch
            {
                "Merger" => DownloadStage.Merging,
                "ExtractAudio" => DownloadStage.ConvertingAudio,
                _ => DownloadStage.Processing,
            };
            progress = new DownloadProgress(stage);
            return true;
        }

        return false;
    }

    private static double? ParseDouble(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static long? ParseLong(string value) => ParseDouble(value) is double result ? (long)result : null;
}
