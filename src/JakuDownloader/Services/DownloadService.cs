using System.Diagnostics;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

public sealed class DownloadService(YtDlpRunner runner, SettingsService settings)
{
    // yt-dlp reporta progreso muchas veces por segundo; mas que esto no se nota en pantalla
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Descarga el video o solo el audio con las opciones elegidas.
    /// </summary>
    /// <remarks>
    /// yt-dlp trabaja solo dentro de <paramref name="workDirectory"/> y despues el archivo final se mueve
    /// al destino. Asi yt-dlp nunca ve los archivos que ya estan en el destino: si viera un video con el
    /// mismo nombre que su archivo intermedio, lo tomaria como propio y lo borraria despues de extraer el
    /// audio. Quien llama decide que hacer con la carpeta de trabajo: borrarla, o conservarla para que
    /// yt-dlp retome una descarga pausada desde los archivos parciales.
    /// </remarks>
    public async Task<DownloadResult> DownloadAsync(
        Uri url,
        string outputDirectory,
        string workDirectory,
        DownloadOptions options,
        IProgress<DownloadProgress>? progress,
        IProgress<VideoInfo>? info,
        CancellationToken cancellationToken)
    {
        var arguments = BuildArguments(url, workDirectory, settings.FileNameTemplate, options, NetworkOptions.DownloadArguments(settings.Current));
        var result = await RunYtDlpAsync(arguments, progress, info, cancellationToken);
        if (!result.Success || result.FilePath is null) return result;

        Directory.CreateDirectory(outputDirectory);
        var destination = MoveToDestination(result.FilePath, outputDirectory);
        MoveCompanions(workDirectory, result.FilePath, destination);
        return DownloadResult.Completed(destination);
    }

    /// <summary>
    /// Mueve los archivos que van junto al video (por ejemplo "Titulo [id].es.srt") con el mismo nombre final.
    /// </summary>
    private static void MoveCompanions(string workDirectory, string originalPath, string destinationPath)
    {
        var originalBase = Path.GetFileNameWithoutExtension(originalPath);
        var finalBase = Path.GetFileNameWithoutExtension(destinationPath);
        var outputDirectory = Path.GetDirectoryName(destinationPath)!;

        foreach (var file in Directory.EnumerateFiles(workDirectory, originalBase + ".*").ToList())
        {
            var suffix = Path.GetFileName(file)[originalBase.Length..];
            if (suffix.EndsWith(".part", StringComparison.OrdinalIgnoreCase) || suffix.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)) continue;

            var target = Path.Combine(outputDirectory, finalBase + suffix);
            for (var copy = 2; File.Exists(target); copy++)
            {
                target = Path.Combine(outputDirectory, $"{finalBase} ({copy}){suffix}");
            }

            File.Move(file, target);
        }
    }

    /// <summary>
    /// Crea una carpeta de trabajo nueva dentro de la carpeta oculta .jaku-temp del destino.
    /// </summary>
    public static string CreateWorkDirectory(string outputDirectory)
    {
        var workDirectory = Path.Combine(AppPaths.GetTempRoot(outputDirectory), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var tempRoot = new DirectoryInfo(AppPaths.GetTempRoot(outputDirectory));
        tempRoot.Attributes |= FileAttributes.Hidden;
        return workDirectory;
    }

    /// <summary>
    /// Borra una carpeta de trabajo y, si quedo vacia, la carpeta .jaku-temp que la contiene.
    /// </summary>
    public static async Task DeleteWorkDirectoryAsync(string workDirectory)
    {
        // Justo despues de matar el proceso Windows puede tardar un poco en liberar los archivos
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(workDirectory)) Directory.Delete(workDirectory, recursive: true);
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var tempRoot = Path.GetDirectoryName(workDirectory);
            if (tempRoot is not null && Directory.Exists(tempRoot) && !Directory.EnumerateFileSystemEntries(tempRoot).Any())
            {
                Directory.Delete(tempRoot);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Borra restos de descargas que quedaron a medias, por ejemplo si la app se cerro de golpe.
    /// </summary>
    public static void CleanupStaleTempFiles(string outputDirectory)
    {
        try
        {
            var tempRoot = AppPaths.GetTempRoot(outputDirectory);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private async Task<DownloadResult> RunYtDlpAsync(
        IEnumerable<string> arguments,
        IProgress<DownloadProgress>? progress,
        IProgress<VideoInfo>? info,
        CancellationToken cancellationToken)
    {
        using var process = runner.Start(arguments);

        var outputTask = ReadOutputAsync(process.StandardOutput, progress, info);
        var errorTask = ReadErrorAsync(process.StandardError, progress);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            YtDlpRunner.Kill(process);

            // Esperar a que suelte los archivos antes de que se borre o se retome la carpeta de trabajo
            await process.WaitForExitAsync();
            await Task.WhenAll((Task)outputTask, errorTask);
            throw;
        }

        var filePath = await outputTask;
        var error = await errorTask;

        // Con --ignore-errors yt-dlp termina con error si fallo un subtitulo, aunque el video este completo
        if (filePath is not null && File.Exists(filePath)) return DownloadResult.Completed(filePath);

        if (process.ExitCode != 0)
        {
            var rawError = YtDlpRunner.ExtractError(error) ?? $"yt-dlp terminó con código {process.ExitCode}.";
            return DownloadResult.Failed(YtDlpErrorTranslator.TryTranslate(rawError), rawError);
        }

        return DownloadResult.Completed(filePath);
    }

    /// <summary>
    /// Mueve el archivo terminado al destino sin pisar nada: si el nombre ya existe, agrega " (2)", " (3)"...
    /// </summary>
    private static string MoveToDestination(string sourcePath, string outputDirectory)
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var destination = Path.Combine(outputDirectory, name + extension);

        for (var copy = 2; File.Exists(destination); copy++)
        {
            destination = Path.Combine(outputDirectory, $"{name} ({copy}){extension}");
        }

        // La carpeta de trabajo esta dentro del destino: es el mismo disco y el movimiento es instantaneo
        File.Move(sourcePath, destination);
        return destination;
    }

    private static IEnumerable<string> BuildArguments(
        Uri url, string workDirectory, string outputTemplate, DownloadOptions options, IEnumerable<string> networkArguments)
    {
        yield return "--no-mtime";

        foreach (var argument in FormatSelection.BuildArguments(options).Concat(networkArguments))
        {
            yield return argument;
        }

        yield return "-P";
        yield return workDirectory;
        yield return "-o";
        yield return outputTemplate;

        // --print silencia la salida normal; --progress la vuelve a activar con nuestro formato
        yield return "--print";
        yield return YtDlpOutput.InfoTemplate;
        yield return "--print";
        yield return YtDlpOutput.FilePathTemplate;
        yield return "--progress";
        yield return "--newline";
        yield return "--progress-template";
        yield return YtDlpOutput.DownloadTemplate;
        yield return "--progress-template";
        yield return YtDlpOutput.PostprocessTemplate;

        yield return "--";
        yield return url.AbsoluteUri;
    }

    /// <summary>
    /// Lee la salida de errores. Ahi yt-dlp escribe tambien el avance de los postprocesos (unir, convertir,
    /// incrustar, SponsorBlock), asi que esas lineas se reportan como progreso y el resto se junta para el mensaje de error.
    /// </summary>
    private static async Task<string> ReadErrorAsync(StreamReader reader, IProgress<DownloadProgress>? progress)
    {
        var text = new System.Text.StringBuilder();
        while (await reader.ReadLineAsync() is { } line)
        {
            if (YtDlpOutput.TryParseProgress(line, out var update))
            {
                progress?.Report(update);
            }
            else
            {
                text.AppendLine(line);
            }
        }

        return text.ToString();
    }

    private static async Task<string?> ReadOutputAsync(StreamReader reader, IProgress<DownloadProgress>? progress, IProgress<VideoInfo>? info)
    {
        string? filePath = null;
        DownloadStage? lastStage = null;
        var sinceLastReport = Stopwatch.StartNew();

        while (await reader.ReadLineAsync() is { } line)
        {
            if (YtDlpOutput.TryParseFilePath(line, out var path))
            {
                filePath = path;
            }
            else if (YtDlpOutput.TryParseInfo(line, out var videoInfo))
            {
                info?.Report(videoInfo);
            }
            else if (progress is not null && YtDlpOutput.TryParseProgress(line, out var update))
            {
                // Los cambios de etapa siempre se reportan, el resto se limita
                var isComplete = update.Percent >= 100;
                if (update.Stage != lastStage || isComplete || sinceLastReport.Elapsed >= ProgressInterval)
                {
                    progress.Report(update);
                    lastStage = update.Stage;
                    sinceLastReport.Restart();
                }
            }
        }

        return filePath;
    }
}
