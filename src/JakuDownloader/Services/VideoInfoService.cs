using System.Text.Json;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

/// <summary>
/// Obtiene la informacion de un video, o la lista de videos de una lista de reproduccion o un canal, sin descargarlos.
/// </summary>
public sealed class VideoInfoService(YtDlpRunner runner)
{
    /// <summary>
    /// Maximo de videos que se cargan de una lista o canal. 500 tardan unos 5 segundos; un canal grande
    /// completo puede tardar minutos.
    /// </summary>
    public const int MaxPlaylistEntries = 500;

    // Solo los campos necesarios: el JSON completo de yt-dlp pesa varios cientos de KB
    private const string Fields = "%(.{id,title,channel,uploader,duration,thumbnail,live_status})j";

    private const string GenericError = "No se pudo obtener la información del video.";
    private const string GenericPlaylistError = "No se pudo obtener la lista de videos.";

    // Videos que YouTube lista pero no se pueden bajar
    private static readonly string[] UnavailableTitles = ["[Private video]", "[Deleted video]"];

    public async Task<VideoInfoResult> GetInfoAsync(Uri url, CancellationToken cancellationToken)
    {
        var (exitCode, output, error) = await RunAsync(["--no-warnings", "--skip-download", "-O", Fields, "--", url.AbsoluteUri], cancellationToken);

        if (exitCode != 0)
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
    /// Lista los videos sin entrar a cada uno (--flat-playlist), hasta <see cref="MaxPlaylistEntries"/>.
    /// </summary>
    public async Task<PlaylistResult> GetPlaylistAsync(Uri url, CancellationToken cancellationToken)
    {
        // --yes-playlist va despues del --no-playlist comun y lo reemplaza
        var (exitCode, output, error) = await RunAsync(
            ["--no-warnings", "--flat-playlist", "--yes-playlist", "-I", $"1:{MaxPlaylistEntries}", "-J", "--", url.AbsoluteUri],
            cancellationToken);

        if (exitCode != 0)
        {
            var rawError = YtDlpRunner.ExtractError(error) ?? string.Empty;
            return PlaylistResult.Failed(YtDlpErrorTranslator.TryTranslate(rawError) ?? GenericPlaylistError);
        }

        return TryParsePlaylist(output, out var playlist)
            ? PlaylistResult.Found(playlist)
            : PlaylistResult.Failed(GenericPlaylistError);
    }

    private static bool TryParsePlaylist(string json, out PlaylistInfo playlist)
    {
        playlist = null!;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("entries", out var entriesElement) || entriesElement.ValueKind != JsonValueKind.Array) return false;

            var entries = new List<PlaylistEntry>();
            foreach (var entry in entriesElement.EnumerateArray())
            {
                var id = GetString(entry, "id");
                var title = GetString(entry, "title");
                if (id is null || title is null || UnavailableTitles.Contains(title)) continue;

                // Las pestanas de un canal tambien vienen como entradas; solo interesan los videos
                if (GetString(entry, "_type") == "playlist") continue;

                entries.Add(new PlaylistEntry(
                    id,
                    title,
                    GetString(entry, "url") ?? $"https://www.youtube.com/watch?v={id}",
                    entry.TryGetProperty("duration", out var duration) && duration.ValueKind == JsonValueKind.Number ? (int)duration.GetDouble() : null,
                    $"https://i.ytimg.com/vi/{id}/mqdefault.jpg"));
            }

            int? total = root.TryGetProperty("playlist_count", out var count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : null;
            var rawCount = entriesElement.GetArrayLength();
            playlist = new PlaylistInfo(
                GetString(root, "title") ?? "Lista sin título",
                GetString(root, "channel") ?? GetString(root, "uploader"),
                entries,
                total,
                IsTruncated: total is int known ? known > rawCount : rawCount >= MaxPlaylistEntries);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        using var process = runner.Start(arguments);

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

        return (process.ExitCode, await outputTask, await errorTask);
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
