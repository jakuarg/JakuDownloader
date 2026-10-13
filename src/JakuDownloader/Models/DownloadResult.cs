namespace JakuDownloader.Models;

/// <summary>
/// Resultado de una descarga. En los errores, <see cref="ErrorMessage"/> es el texto para el usuario
/// (null si el error no se reconoce) y <see cref="ErrorDetails"/> el mensaje tecnico original.
/// </summary>
public sealed record DownloadResult(bool Success, string? FilePath, string? ErrorMessage, string? ErrorDetails)
{
    public static DownloadResult Completed(string? filePath) => new(true, filePath, null, null);

    public static DownloadResult Failed(string? errorMessage, string errorDetails) => new(false, null, errorMessage, errorDetails);
}
