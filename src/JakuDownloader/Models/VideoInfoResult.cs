namespace JakuDownloader.Models;

public sealed record VideoInfoResult(VideoInfo? Info, string? ErrorMessage)
{
    public static VideoInfoResult Found(VideoInfo info) => new(info, null);

    public static VideoInfoResult Failed(string errorMessage) => new(null, errorMessage);
}
