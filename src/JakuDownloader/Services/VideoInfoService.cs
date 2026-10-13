using System.Text.Json;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

/// <summary>
/// Obtiene titulo, canal, duracion y miniatura de un video sin descargarlo.
/// </summary>
public sealed class VideoInfoService(YtDlpRunner runner)
{
    // Solo los campos necesarios: el JSON completo de yt-dlp pesa varios cientos de KB
    private const string Fields = "%(.{id,title,channel,uploader,duration,thumbnail,live_status})j";

    private const string GenericError = "No se pudo obtener la información del video.";

    public async Task<VideoInfoResult> GetInfoAsync(Uri url, CancellationToken cancellationToken)
    {
        using var process = runner.Start(["--no-warnings", "--skip-download", "-O", Fields, "--", url.AbsoluteUri]);

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            YtDlpRunner.Kill(process);
            await process.WaitForExitAsync();
            await Task.WhenAll(outputTask, errorTask);
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            var rawError = YtDlpRunner.ExtractError(error) ?? string.Empty;
            return VideoInfoResult.Failed(YtDlpErrorTranslator.TryTranslate(rawError) ?? GenericError);
        }

        var json = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return json is not null && TryParse(json, out var info)
            ? VideoInfoResult.Found(info)
            : VideoInfoResult.Failed(GenericError);
    }

    /// <summary>
    /// Interpreta el JSON reducido que imprime yt-dlp con los campos de <see cref="Fields"/>.
    /// </summary>
    public static bool TryParse(string json, out VideoInfo info)
    {
        info = null!;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var liveStatus = GetString(root, "live_status");
            info = new VideoInfo(
                GetString(root, "id") ?? string.Empty,
                GetString(root, "title") ?? "Sin título",
                GetString(root, "channel") ?? GetString(root, "uploader"),
                root.TryGetProperty("duration", out var duration) && duration.ValueKind == JsonValueKind.Number
                    ? (int)duration.GetDouble()
                    : null,
                GetString(root, "thumbnail"),
                liveStatus == "is_live",
                liveStatus == "is_upcoming");
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
