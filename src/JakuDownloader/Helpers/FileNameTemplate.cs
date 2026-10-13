using System.Text.RegularExpressions;

namespace JakuDownloader.Helpers;

/// <summary>
/// Plantilla de yt-dlp para el nombre de los archivos: validacion y ejemplo.
/// </summary>
public static partial class FileNameTemplate
{
    public const string Default = "%(title)s [%(id)s].%(ext)s";

    private const string ExtensionField = ".%(ext)s";

    private static readonly char[] InvalidLiteralChars = ['<', '>', ':', '"', '|', '?', '*', '/', '\\'];

    private static readonly Dictionary<string, string> SampleValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "Título del video",
        ["id"] = "dQw4w9WgXcQ",
        ["uploader"] = "Canal",
        ["channel"] = "Canal",
        ["upload_date"] = "20261013",
        ["resolution"] = "1920x1080",
        ["height"] = "1080",
    };

    /// <summary>
    /// Devuelve null si la plantilla es valida, o el motivo para mostrar si no lo es.
    /// </summary>
    public static string? Validate(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return "La plantilla no puede estar vacía.";

        if (!template.EndsWith(ExtensionField, StringComparison.Ordinal))
        {
            return "Tiene que terminar en .%(ext)s para que el archivo tenga extensión.";
        }

        // Lo que no es un campo %(...)s queda tal cual en el nombre del archivo
        var literal = Field().Replace(template, string.Empty);
        if (literal.IndexOfAny(['/', '\\']) >= 0) return "Por ahora no se admiten subcarpetas.";
        if (literal.IndexOfAny(InvalidLiteralChars) >= 0) return "No puede tener los caracteres < > : \" | ? *";

        var fields = Field().Matches(template).Select(match => match.Groups["name"].Value).ToList();
        if (fields.All(name => name.Equals("ext", StringComparison.OrdinalIgnoreCase)))
        {
            return "Incluí algún dato del video, como %(title)s o %(id)s, para que cada archivo tenga un nombre distinto.";
        }

        return null;
    }

    /// <summary>
    /// Nombre de ejemplo con datos ficticios, para mostrar como va a quedar.
    /// </summary>
    public static string Preview(string template, string extension = "mp4")
        => Field().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            if (name.Equals("ext", StringComparison.OrdinalIgnoreCase)) return extension;
            return SampleValues.TryGetValue(name, out var value) ? value : "NA";
        });

    // Campo de yt-dlp: %(nombre)s, %(nombre)d, %(nombre)05d, etc.
    [GeneratedRegex(@"%\((?<name>[^)]*)\)[-#0+ ]*\d*(?:\.\d+)?[a-zA-Z]")]
    private static partial Regex Field();
}
