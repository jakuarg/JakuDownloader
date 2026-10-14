namespace JakuDownloader.Helpers;

/// <summary>
/// Categorias de SponsorBlock que ofrece la app: el identificador que usa yt-dlp y el nombre para mostrar.
/// </summary>
public static class SponsorBlockCategories
{
    public const string Default = "sponsor";

    public static IReadOnlyList<(string Id, string Label)> All { get; } =
    [
        ("sponsor", "Patrocinio"),
        ("selfpromo", "Autopromoción"),
        ("interaction", "Pedido de suscripción"),
        ("intro", "Intro"),
        ("outro", "Créditos finales"),
        ("preview", "Avance"),
        ("music_offtopic", "Partes sin música"),
        ("filler", "Relleno"),
    ];
}
