namespace JakuDownloader.Models;

public enum SponsorBlockMode
{
    Off,

    /// <summary>Los segmentos se agregan como capitulos para saltarlos desde el reproductor.</summary>
    Mark,

    /// <summary>Los segmentos se cortan del archivo.</summary>
    Remove,
}
