namespace JakuDownloader.Helpers;

/// <summary>
/// Interruptores de las funciones del roadmap. La interfaz ya tiene todos los controles;
/// mientras una funcion este en false se muestra deshabilitada con la version en la que llega.
/// </summary>
public static class Features
{
    /// <summary>v00.03.00: vista previa, video o solo audio, mp3.</summary>
    public static bool VideoInfo => true;

    /// <summary>v00.04.00: resolucion, formato de salida y codecs.</summary>
    public static bool QualityAndFormats => true;

    /// <summary>v00.05.00: carpeta de destino, plantilla de nombre y gestion de herramientas.</summary>
    public static bool Settings => true;

    /// <summary>v00.06.00: cola de descargas e historial.</summary>
    public static bool Queue => true;

    /// <summary>v00.07.00: listas de reproduccion y canales.</summary>
    public static bool Playlists => true;

    /// <summary>v00.08.00: subtitulos, capitulos, miniatura y metadatos.</summary>
    public static bool Subtitles => true;

    /// <summary>v00.09.00: SponsorBlock.</summary>
    public static bool SponsorBlock => true;

    /// <summary>v00.10.00: cookies, proxy y limite de velocidad.</summary>
    public static bool Network => true;

    /// <summary>v01.xx.xx: otros sitios ademas de YouTube.</summary>
    public static bool OtherSites => false;
}
