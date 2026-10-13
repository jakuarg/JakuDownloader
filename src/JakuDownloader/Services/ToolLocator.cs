using JakuDownloader.Helpers;

namespace JakuDownloader.Services;

/// <summary>
/// Ubica yt-dlp, FFmpeg y Deno dentro de la carpeta tools junto al ejecutable.
/// </summary>
public sealed class ToolLocator
{
    public string ToolsDirectory => AppPaths.ToolsDirectory;

    public string YtDlpPath => Path.Combine(ToolsDirectory, "yt-dlp.exe");

    public string FfmpegPath => Path.Combine(ToolsDirectory, "ffmpeg.exe");

    public string DenoPath => Path.Combine(ToolsDirectory, "deno.exe");

    public bool HasDeno => File.Exists(DenoPath);

    // Deno no es obligatorio todavia: sin el, yt-dlp puede seguir bajando algunos formatos
    public IReadOnlyList<string> GetMissingTools()
    {
        var missing = new List<string>();
        if (!File.Exists(YtDlpPath)) missing.Add("yt-dlp.exe");
        if (!File.Exists(FfmpegPath)) missing.Add("ffmpeg.exe");
        return missing;
    }
}
