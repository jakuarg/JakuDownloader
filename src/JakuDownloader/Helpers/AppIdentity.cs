namespace JakuDownloader.Helpers;

public static class AppIdentity
{
    /// <summary>
    /// Identifica a la app ante Windows: agrupa la barra de tareas y vincula los toasts con el acceso directo de Inicio.
    /// </summary>
    public const string AppUserModelId = "JakuDownloader";

    /// <summary>
    /// Version en el formato del README: v00.02.00.
    /// </summary>
    public static string DisplayVersion { get; } = FormatVersion(typeof(AppIdentity).Assembly.GetName().Version);

    private static string FormatVersion(Version? version)
        => version is null ? "v00.00.00" : $"v{version.Major:00}.{version.Minor:00}.{Math.Max(version.Build, 0):00}";

    public static void Apply() => NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
}
