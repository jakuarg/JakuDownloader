namespace JakuDownloader.Models;

/// <summary>
/// Codecs de video que ofrece YouTube. H.265 no esta: YouTube no lo sirve.
/// </summary>
public enum VideoCodec
{
    Auto,
    H264,
    Vp9,
    Av1,
}
