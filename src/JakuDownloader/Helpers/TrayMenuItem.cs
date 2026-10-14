namespace JakuDownloader.Helpers;

/// <summary>
/// Opcion del menu del icono junto al reloj. <see cref="Separator"/> es la linea divisoria.
/// </summary>
public sealed record TrayMenuItem(string Text, Action? Action, bool IsEnabled = true, bool IsDefault = false)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, null);

    public bool IsSeparator => Action is null;
}
