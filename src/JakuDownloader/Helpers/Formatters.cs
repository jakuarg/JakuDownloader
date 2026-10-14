namespace JakuDownloader.Helpers;

public static class Formatters
{
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

    public static string FormatBytes(double bytes)
    {
        var unit = 0;
        while (bytes >= 1024 && unit < SizeUnits.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:0} {SizeUnits[unit]}" : $"{bytes:0.0} {SizeUnits[unit]}";
    }

    public static string FormatDuration(int seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }

    /// <summary>
    /// "a", "a y b", "a, b y c".
    /// </summary>
    public static string JoinWithAnd(IReadOnlyList<string> items) => items.Count <= 1
        ? string.Concat(items)
        : $"{string.Join(", ", items.Take(items.Count - 1))} y {items[^1]}";
}
