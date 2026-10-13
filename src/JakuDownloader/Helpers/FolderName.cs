namespace JakuDownloader.Helpers;

public static class FolderName
{
    // Nombres que Windows reserva y no se pueden usar como carpeta
    private static readonly string[] ReservedNames =
        ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

    /// <summary>
    /// Convierte un titulo en un nombre de carpeta valido: reemplaza caracteres prohibidos y recorta el largo.
    /// </summary>
    public static string FromTitle(string title, string fallback = "Lista")
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
        if (cleaned.Length > 100) cleaned = cleaned[..100].TrimEnd('.', ' ');

        return cleaned.Length == 0 || ReservedNames.Contains(cleaned, StringComparer.OrdinalIgnoreCase) ? fallback : cleaned;
    }
}
