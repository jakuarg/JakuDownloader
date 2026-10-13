namespace JakuDownloader.Helpers;

/// <summary>
/// Convierte los errores de yt-dlp en mensajes que se entiendan sin conocer la herramienta.
/// </summary>
/// <remarks>
/// Cada mensaje propone algo que se pueda hacer con la version actual de la app,
/// o aclara cuando la solucion todavia no esta disponible.
/// </remarks>
public static class YtDlpErrorTranslator
{
    // El orden importa: los casos mas especificos van primero
    private static readonly (string[] Patterns, string Message)[] KnownErrors =
    [
        (["Private video"], "El video es privado. Solo se puede bajar si el autor lo hace público."),
        (["playlist does not exist", "This playlist is private", "The playlist is private"],
            "La lista no existe o es privada. Revisá que el link sea correcto."),
        (["confirm your age", "age-restricted", "inappropriate for some users"],
            "El video tiene restricción de edad y pide iniciar sesión. Todavía no se puede bajar: el uso de cookies llega en la v00.10."),
        (["not a bot"], "YouTube pidió confirmar que no sos un bot. Esperá unos minutos y volvé a intentar."),
        (["members-only", "Join this channel"],
            "El video es solo para miembros del canal. Todavía no se puede bajar: el uso de cookies llega en la v00.10."),
        (["Premieres in", "live event will begin", "This live event"], "El video todavía no se estrenó. Volvé a intentar cuando esté publicado."),
        (["removed by the uploader", "account associated with this video has been terminated", "copyright"],
            "El video fue eliminado y ya no se puede bajar."),
        (["not available in your country", "blocked it in your country"], "El video no está disponible en tu país."),
        (["Video unavailable", "This video is unavailable", "This video is not available"],
            "El video no está disponible. Revisá que el link sea correcto."),
        (["is not a valid URL", "Unsupported URL", "Incomplete YouTube ID"],
            "El link no es válido o el sitio no está soportado. Por ahora solo funciona YouTube."),
        (["HTTP Error 429", "Too Many Requests"], "YouTube está limitando las descargas. Esperá un rato y volvé a intentar."),
        (["HTTP Error 403"], "YouTube rechazó la descarga. Volvé a intentar en unos minutos."),
        (["Requested format is not available"], "Este video no tiene un formato que se pueda bajar como mp4."),
        (["getaddrinfo failed", "Failed to resolve", "Name or service not known", "No connection could be made",
          "Network is unreachable", "timed out", "Unable to download webpage", "Connection reset", "Remote end closed"],
            "No hay conexión a internet o el sitio no responde. Revisá la conexión y volvé a intentar."),
        (["No space left on device", "There is not enough space"], "No hay espacio suficiente en el disco. Liberá espacio y volvé a intentar."),
    ];

    /// <summary>
    /// Devuelve el mensaje para el usuario, o null si el error no es uno conocido.
    /// </summary>
    public static string? TryTranslate(string rawError)
    {
        foreach (var (patterns, message) in KnownErrors)
        {
            if (patterns.Any(pattern => rawError.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return message;
            }
        }

        return null;
    }
}
