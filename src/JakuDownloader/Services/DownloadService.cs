using System.Diagnostics;
using System.Text;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

public sealed class DownloadService(ToolLocator toolLocator)
{
    private const string OutputTemplate = "%(title)s [%(id)s].%(ext)s";

    /// <summary>
    /// Descarga el video en la mejor calidad disponible como mp4.
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(Uri url, string outputDirectory, CancellationToken cancellationToken)
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
        foreach (var argument in BuildArguments(url, outputDirectory))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // yt-dlp lanza ffmpeg como hijo, hay que matar el arbol completo
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            return DownloadResult.Failed(ExtractError(error) ?? $"yt-dlp terminó con código {process.ExitCode}.");
        }

        return DownloadResult.Completed(LastLine(output));
    }

    private IEnumerable<string> BuildArguments(Uri url, string outputDirectory)
    {
        yield return "--encoding";
        yield return "utf-8";
        yield return "--no-playlist";
        yield return "--no-mtime";

        yield return "-f";
        yield return "bv*+ba/b";
        yield return "--merge-output-format";
        yield return "mp4";

        yield return "--ffmpeg-location";
        yield return toolLocator.ToolsDirectory;
        if (toolLocator.HasDeno)
        {
            yield return "--js-runtimes";
            yield return $"deno:{toolLocator.DenoPath}";
        }

        yield return "-P";
        yield return outputDirectory;
        yield return "-o";
        yield return OutputTemplate;

        // Imprime la ruta final del archivo cuando termina de moverlo
        yield return "--print";
        yield return "after_move:filepath";

        yield return "--";
        yield return url.AbsoluteUri;
    }

    private static string? ExtractError(string stderr)
    {
        var lines = SplitLines(stderr);
        var error = lines.LastOrDefault(line => line.StartsWith("ERROR:", StringComparison.Ordinal));
        return error is not null ? error["ERROR:".Length..].Trim() : lines.LastOrDefault();
    }

    private static string? LastLine(string text) => SplitLines(text).LastOrDefault();

    private static string[] SplitLines(string text)
        => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
