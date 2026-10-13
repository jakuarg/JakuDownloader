namespace JakuDownloader.Helpers;

public static class AppIdentity
{
    /// <summary>
    /// Identifica a la app ante Windows: agrupa la barra de tareas y vincula los toasts con el acceso directo de Inicio.
    /// </summary>
    public const string AppUserModelId = "JakuDownloader";

    public static void Apply() => NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
}
