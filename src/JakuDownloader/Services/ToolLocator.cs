using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

/// <summary>
/// Ubica yt-dlp, FFmpeg y Deno. Busca primero en la carpeta tools junto al ejecutable y despues en
/// la del usuario, que se usa cuando la app esta en una carpeta sin permiso de escritura.
/// </summary>
public sealed class ToolLocator
{
    private const string VersionsFileName = "versions.txt";

    private static readonly string[] SearchDirectories = [AppPaths.ToolsDirectory, AppPaths.UserToolsDirectory];

    public string YtDlpPath => Find("yt-dlp.exe");

    public string FfmpegPath => Find("ffmpeg.exe");

    /// <summary>
    /// Carpeta de ffmpeg.exe; ffprobe y sus DLL estan siempre en la misma.
    /// </summary>
    public string FfmpegDirectory => Path.GetDirectoryName(FfmpegPath)!;

    public string DenoPath => Find("deno.exe");

    public bool HasDeno => File.Exists(DenoPath);

    /// <summary>
    /// Carpeta que se abre desde la app: donde esta yt-dlp, o donde se instalarian las herramientas.
    /// </summary>
    public string ToolsDirectory => File.Exists(YtDlpPath) ? Path.GetDirectoryName(YtDlpPath)! : InstallDirectory;

    /// <summary>
    /// Donde se instalan las herramientas que faltan: junto al ejecutable si se puede escribir ahi.
    /// </summary>
    public string InstallDirectory => AppPaths.IsWritable(AppPaths.ToolsDirectory) ? AppPaths.ToolsDirectory : AppPaths.UserToolsDirectory;

    public static string DisplayName(Tool tool) => tool switch
    {
        Tool.YtDlp => "yt-dlp",
        Tool.Ffmpeg => "FFmpeg",
        _ => "Deno",
    };

    /// <summary>
    /// Herramientas que faltan. Deno no impide descargar, pero YouTube lo necesita para casi todos los formatos.
    /// </summary>
    public IReadOnlyList<Tool> GetMissing()
    {
        var missing = new List<Tool>();
        if (!File.Exists(YtDlpPath)) missing.Add(Tool.YtDlp);
        if (!File.Exists(FfmpegPath)) missing.Add(Tool.Ffmpeg);
        if (!HasDeno) missing.Add(Tool.Deno);
        return missing;
    }

    /// <summary>
    /// Las que hacen falta si o si para descargar.
    /// </summary>
    public IReadOnlyList<Tool> GetMissingRequired() => GetMissing().Where(tool => tool != Tool.Deno).ToList();

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

    /// <summary>
    /// Anota la version de una herramienta en el versions.txt de su carpeta (el mismo formato que usa scripts\get-tools.ps1).
    /// </summary>
    public static void SaveVersion(string directory, string key, string version)
    {
        var path = Path.Combine(directory, VersionsFileName);
        var versions = File.Exists(path) ? ParseVersions(File.ReadLines(path)) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        versions[key] = version;
        File.WriteAllLines(path, versions.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string Find(string fileName)
    {
        foreach (var directory in SearchDirectories)
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path)) return path;
        }

        return Path.Combine(AppPaths.ToolsDirectory, fileName);
    }

    // La carpeta junto al ejecutable tiene prioridad, igual que al buscar las herramientas
    private static Dictionary<string, string> ReadVersions()
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in SearchDirectories.Reverse())
        {
            var path = Path.Combine(directory, VersionsFileName);
            if (!File.Exists(path)) continue;
            foreach (var (key, value) in ParseVersions(File.ReadLines(path))) versions[key] = value;
        }

        return versions;
    }

    private static Dictionary<string, string> ParseVersions(IEnumerable<string> lines)
        => lines
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last()[1].Trim(), StringComparer.OrdinalIgnoreCase);
}
