using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

/// <summary>
/// Descarga e instala yt-dlp, FFmpeg y Deno desde sus releases oficiales, y actualiza yt-dlp.
/// </summary>
/// <remarks>
/// Cada archivo se verifica contra el SHA-256 publicado en el mismo release antes de instalarlo,
/// igual que scripts\get-tools.ps1.
/// </remarks>
public sealed partial class ToolInstaller(ToolLocator toolLocator)
{
    // BtbN no mantiene builds viejas: se usa la rama estable, que solo recibe parches
    private const string FfmpegBranch = "9.0";
    private const string FfmpegVersionLabel = $"n{FfmpegBranch}-latest-lgpl-shared";

    private const string YtDlpBase = "https://github.com/yt-dlp/yt-dlp/releases/latest/download";
    private const string DenoBase = "https://github.com/denoland/deno/releases/latest/download";
    private const string DenoAsset = "deno-x86_64-pc-windows-msvc.zip";
    private const string FfmpegBase = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest";
    private const string FfmpegAsset = $"ffmpeg-n{FfmpegBranch}-latest-win64-lgpl-shared-{FfmpegBranch}.zip";

    // DLL de FFmpeg: se reemplazan juntas para no mezclar versiones
    private static readonly string[] FfmpegLibraryPrefixes = ["avcodec", "avdevice", "avfilter", "avformat", "avutil", "postproc", "swresample", "swscale"];

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>
    /// Tamano aproximado de cada descarga en MB, para avisar antes de empezar.
    /// </summary>
    public static int ApproximateSizeMegabytes(Tool tool) => tool switch
    {
        Tool.YtDlp => 17,
        Tool.Ffmpeg => 74,
        _ => 41,
    };

    public async Task InstallAsync(IReadOnlyList<Tool> tools, IProgress<ToolInstallProgress> progress, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                "La descarga automática de herramientas por ahora solo funciona en Windows de 64 bits (x64).");
        }

        var target = toolLocator.InstallDirectory;
        Directory.CreateDirectory(target);
        var work = Path.Combine(Path.GetTempPath(), "JakuDownloader", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);

        try
        {
            for (var index = 0; index < tools.Count; index++)
            {
                var tool = tools[index];
                var current = index;
                void Report(ToolInstallStage stage, double? fraction = null)
                    => progress.Report(new ToolInstallProgress(tool, current, tools.Count, stage, fraction));

                switch (tool)
                {
                    case Tool.YtDlp:
                        await InstallYtDlpAsync(target, work, Report, cancellationToken);
                        break;
                    case Tool.Ffmpeg:
                        await InstallFfmpegAsync(target, work, Report, cancellationToken);
                        break;
                    case Tool.Deno:
                        await InstallDenoAsync(target, work, Report, cancellationToken);
                        break;
                }
            }
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Ejecuta "yt-dlp -U". Compara la version antes y despues para saber si hubo actualizacion.
    /// </summary>
    public async Task<YtDlpUpdateResult> UpdateYtDlpAsync(CancellationToken cancellationToken)
    {
        var path = toolLocator.YtDlpPath;
        var before = await GetVersionAsync(path, cancellationToken);
        var (exitCode, _, error) = await RunAsync(path, ["-U"], cancellationToken);

        if (exitCode != 0)
        {
            var rawError = YtDlpRunner.ExtractError(error) ?? $"yt-dlp terminó con código {exitCode}.";
            var message = rawError.Contains("write", StringComparison.OrdinalIgnoreCase) || rawError.Contains("permission", StringComparison.OrdinalIgnoreCase)
                ? "No se puede escribir en la carpeta de yt-dlp. Si la app está en una carpeta protegida, movela a otra."
                : "No se pudo actualizar yt-dlp. Revisá la conexión y volvé a intentar.";
            return new YtDlpUpdateResult(false, before, message, rawError);
        }

        var after = await GetVersionAsync(path, cancellationToken);
        if (after is not null) ToolLocator.SaveVersion(Path.GetDirectoryName(path)!, "yt-dlp", after);
        return new YtDlpUpdateResult(after != before, after, null, null);
    }

    private static async Task InstallYtDlpAsync(string target, string work, Action<ToolInstallStage, double?> report, CancellationToken cancellationToken)
    {
        var file = Path.Combine(work, "yt-dlp.exe");
        await DownloadVerifiedAsync($"{YtDlpBase}/yt-dlp.exe", $"{YtDlpBase}/SHA2-256SUMS", "yt-dlp.exe", file,
            fraction => report(ToolInstallStage.Downloading, fraction), cancellationToken);

        report(ToolInstallStage.Installing, null);
        var destination = Path.Combine(target, "yt-dlp.exe");
        File.Copy(file, destination, overwrite: true);

        var version = await GetVersionAsync(destination, cancellationToken);
        if (version is not null) ToolLocator.SaveVersion(target, "yt-dlp", version);
    }

    private static async Task InstallDenoAsync(string target, string work, Action<ToolInstallStage, double?> report, CancellationToken cancellationToken)
    {
        var zip = Path.Combine(work, DenoAsset);
        await DownloadVerifiedAsync($"{DenoBase}/{DenoAsset}", $"{DenoBase}/{DenoAsset}.sha256sum", DenoAsset, zip,
            fraction => report(ToolInstallStage.Downloading, fraction), cancellationToken);

        report(ToolInstallStage.Installing, null);
        using (var archive = ZipFile.OpenRead(zip))
        {
            var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals("deno.exe", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("El archivo de Deno no tiene deno.exe.");
            entry.ExtractToFile(Path.Combine(target, "deno.exe"), overwrite: true);
        }

        // "deno 2.9.7 (stable, release, x86_64-pc-windows-msvc)" -> "v2.9.7"
        var output = await GetVersionAsync(Path.Combine(target, "deno.exe"), cancellationToken);
        var match = output is null ? Match.Empty : DenoVersion().Match(output);
        if (match.Success) ToolLocator.SaveVersion(target, "deno", "v" + match.Groups[1].Value);
    }

    private static async Task InstallFfmpegAsync(string target, string work, Action<ToolInstallStage, double?> report, CancellationToken cancellationToken)
    {
        var zip = Path.Combine(work, FfmpegAsset);
        await DownloadVerifiedAsync($"{FfmpegBase}/{FfmpegAsset}", $"{FfmpegBase}/checksums.sha256", FfmpegAsset, zip,
            fraction => report(ToolInstallStage.Downloading, fraction), cancellationToken);

        report(ToolInstallStage.Installing, null);
        foreach (var old in Directory.EnumerateFiles(target, "*.dll").Where(IsFfmpegLibrary).ToList())
        {
            File.Delete(old);
        }

        using (var archive = ZipFile.OpenRead(zip))
        {
            // Solo se toma el nombre de cada entrada: nunca se escribe fuera de la carpeta de herramientas
            foreach (var entry in archive.Entries.Where(e => IsInBinFolder(e.FullName)))
            {
                var name = entry.Name;
                if (name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(Path.Combine(target, name), overwrite: true);
                }
            }

            // La licencia LGPL tiene que ir junto a los binarios
            var license = archive.Entries.FirstOrDefault(e => e.Name.Equals("LICENSE.txt", StringComparison.OrdinalIgnoreCase));
            license?.ExtractToFile(Path.Combine(target, "FFMPEG-LICENSE.txt"), overwrite: true);
        }

        if (!File.Exists(Path.Combine(target, "ffmpeg.exe")))
        {
            throw new InvalidDataException("El archivo de FFmpeg no tiene ffmpeg.exe.");
        }

        ToolLocator.SaveVersion(target, "ffmpeg", FfmpegVersionLabel);
    }

    private static bool IsInBinFolder(string entryPath)
        => entryPath.Replace('\\', '/').Split('/') is { Length: >= 2 } parts && parts[^2].Equals("bin", StringComparison.OrdinalIgnoreCase);

    private static bool IsFfmpegLibrary(string path)
        => FfmpegLibraryPrefixes.Any(prefix => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Descarga un archivo calculando su SHA-256 en el camino y lo compara con el publicado.
    /// </summary>
    private static async Task DownloadVerifiedAsync(
        string url,
        string checksumsUrl,
        string assetName,
        string destination,
        Action<double?> onProgress,
        CancellationToken cancellationToken)
    {
        var expected = await GetExpectedHashAsync(checksumsUrl, assetName, cancellationToken);

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var file = File.Create(destination))
        {
            var buffer = new byte[81920];
            long done = 0;
            var lastReport = Stopwatch.StartNew();
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                done += read;

                if (lastReport.ElapsedMilliseconds >= 150)
                {
                    onProgress(total is > 0 ? (double)done / total.Value : null);
                    lastReport.Restart();
                }
            }
        }

        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (actual != expected)
        {
            File.Delete(destination);
            throw new InvalidDataException($"El archivo {assetName} no coincide con la firma publicada y no se instaló. Volvé a intentar más tarde.");
        }
    }

    // Formatos de los releases: "hash  nombre" por linea, o un archivo con un solo hash
    private static async Task<string> GetExpectedHashAsync(string checksumsUrl, string assetName, CancellationToken cancellationToken)
    {
        var content = await Http.GetStringAsync(checksumsUrl, cancellationToken);
        var lines = content.Split('\n').Where(line => Sha256().IsMatch(line)).ToList();
        var line = lines.FirstOrDefault(l => l.TrimEnd().EndsWith(assetName, StringComparison.OrdinalIgnoreCase))
            ?? (lines.Count == 1 ? lines[0] : null)
            ?? throw new InvalidDataException($"No se encontró la firma de {assetName}.");
        return Sha256().Match(line).Value.ToLowerInvariant();
    }

    private static async Task<string?> GetVersionAsync(string executable, CancellationToken cancellationToken)
    {
        var (exitCode, output, _) = await RunAsync(executable, ["--version"], cancellationToken);
        return exitCode == 0 ? output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() : null;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JakuDownloader");
        return client;
    }

    [GeneratedRegex("[0-9a-fA-F]{64}")]
    private static partial Regex Sha256();

    [GeneratedRegex(@"deno (\d+\.\d+\.\d+)")]
    private static partial Regex DenoVersion();
}
