using JakuDownloader.Helpers;
using JakuDownloader.Models;

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

    public IReadOnlyList<ToolInfo> GetToolInfos()
    {
        var versions = ReadVersions();
        return
        [
            new("yt-dlp", "Obtiene la información y descarga", versions.GetValueOrDefault("yt-dlp"), File.Exists(YtDlpPath)),
            new("FFmpeg", "Une, convierte e incrusta", versions.GetValueOrDefault("ffmpeg"), File.Exists(FfmpegPath)),
            new("Deno", "Runtime de JavaScript que yt-dlp necesita para YouTube", versions.GetValueOrDefault("deno"), HasDeno),
        ];
    }

    // versions.txt lo escribe scripts\get-tools.ps1 con lineas "nombre=version"
    private Dictionary<string, string> ReadVersions()
    {
        var path = Path.Combine(ToolsDirectory, "versions.txt");
        if (!File.Exists(path)) return [];

        return File.ReadLines(path)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
