using System.Text.RegularExpressions;

namespace JakuDownloader.Helpers;

/// <summary>
/// Idiomas de subtitulos que escribe el usuario ("es, en, es-419").
/// </summary>
/// <remarks>
/// Se pasan a yt-dlp como codigos exactos. Un patron como "es.*" tambien trae las traducciones
/// automaticas de YouTube ("es-en": espanol traducido del ingles), que son decenas de pedidos y
/// terminan en "429 Too Many Requests". yt-dlp compara sin distinguir mayusculas, asi que no hay
/// forma de separar regiones ("en-US") de traducciones ("en-de") con un patron.
/// </remarks>
public static partial class SubtitleLanguages
{
    public const string Default = "es, en";

    public static IReadOnlyList<string> Parse(string text)
        => text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Devuelve null si la lista es valida, o el motivo para mostrar si no lo es.
    /// </summary>
    public static string? Validate(string text)
    {
        var codes = Parse(text);
        if (codes.Count == 0) return "Escribí al menos un idioma, por ejemplo es.";

        var invalid = codes.FirstOrDefault(code => !LanguageCode().IsMatch(code));
        return invalid is null ? null : $"\"{invalid}\" no es un código de idioma. Usá códigos como es, en o es-419.";
    }

    /// <summary>
    /// Formato para --sub-langs: codigos separados por coma, sin espacios.
    /// </summary>
    public static string Normalize(string text) => string.Join(',', Parse(text));

    // Idioma de 2 o 3 letras con region opcional: es, spa, es-419, pt-BR, zh-Hans
    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,4})?$")]
    private static partial Regex LanguageCode();
}
