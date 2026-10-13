namespace JakuDownloader.Models;

public sealed record PlaylistResult(PlaylistInfo? Info, string? ErrorMessage)
{
    public static PlaylistResult Found(PlaylistInfo info) => new(info, null);

    public static PlaylistResult Failed(string errorMessage) => new(null, errorMessage);
}
