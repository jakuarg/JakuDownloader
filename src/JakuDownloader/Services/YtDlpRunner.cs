using System.Diagnostics;
using System.Text;

namespace JakuDownloader.Services;

/// <summary>
/// Lanza yt-dlp con las opciones que comparten todas las operaciones.
/// </summary>
public sealed class YtDlpRunner(ToolLocator toolLocator)
{
    /// <summary>
    /// Inicia yt-dlp. Los argumentos propios van despues de los comunes; el link tiene que ir al final, despues de "--".
    /// </summary>
    public Process Start(IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(toolLocator.YtDlpPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in CommonArguments().Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        return process;
    }

    /// <summary>
    /// Mata yt-dlp junto con sus hijos (ffmpeg, deno).
    /// </summary>
    public static void Kill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Devuelve la ultima linea "ERROR:" de yt-dlp, o la ultima linea si no hay ninguna.
    /// </summary>
    public static string? ExtractError(string stderr)
    {
        var lines = stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var error = lines.LastOrDefault(line => line.StartsWith("ERROR:", StringComparison.Ordinal));
        return error is not null ? error["ERROR:".Length..].Trim() : lines.LastOrDefault();
    }

    private IEnumerable<string> CommonArguments()
    {
        yield return "--encoding";
        yield return "utf-8";
        yield return "--no-playlist";

        yield return "--ffmpeg-location";
        yield return toolLocator.ToolsDirectory;
        if (toolLocator.HasDeno)
        {
            yield return "--js-runtimes";
            yield return $"deno:{toolLocator.DenoPath}";
        }
    }
}
