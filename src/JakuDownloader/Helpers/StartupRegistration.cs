using System.Security;
using Microsoft.Win32;

namespace JakuDownloader.Helpers;

/// <summary>
/// Inicio con Windows: un valor en la clave Run del usuario, que no pide permisos de administrador.
/// </summary>
public static class StartupRegistration
{
    /// <summary>
    /// Argumento con el que Windows abre la app al iniciar sesion: arranca oculta, solo con el icono junto al reloj.
    /// </summary>
    public const string BackgroundArgument = "--background";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Donde el Administrador de tareas anota los programas de inicio que el usuario deshabilito
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private const string ValueName = "Jaku Downloader";

    private static string Command => $"\"{Environment.ProcessPath}\" {BackgroundArgument}";

    /// <summary>
    /// True si Windows va a abrir la app al iniciar sesion.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            if (run?.GetValue(ValueName) is not string) return false;

            // El primer byte impar significa deshabilitado desde el Administrador de tareas
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } state || (state[0] & 1) == 0;
        }
    }

    public static void Enable()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            run.SetValue(ValueName, Command);
        }

        // Si lo habian deshabilitado desde el Administrador de tareas, activarlo aca tiene que valer
        DeleteApproval();
    }

    public static void Disable()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            run?.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        DeleteApproval();
    }

    /// <summary>
    /// Si la app se movio de carpeta, el valor guardado apunta al ejecutable viejo y Windows no la encontraria.
    /// </summary>
    public static void TryRefreshPath()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(ValueName) is string current && current != Command) run.SetValue(ValueName, Command);
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
            // Se vuelve a intentar en el proximo inicio
        }
    }

    public static bool IsRegistryError(Exception ex) => ex is IOException or UnauthorizedAccessException or SecurityException;

    private static void DeleteApproval()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
