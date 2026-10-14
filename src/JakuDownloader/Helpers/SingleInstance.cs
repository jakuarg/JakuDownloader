using System.Diagnostics;

namespace JakuDownloader.Helpers;

/// <summary>
/// Una sola ventana de la app a la vez.
/// </summary>
/// <remarks>
/// Dos instancias se pisarian: al arrancar se borran los restos de .jaku-temp (que serian las descargas
/// en curso de la otra) y las dos escribirian el mismo historial y la misma configuracion.
/// </remarks>
public static class SingleInstance
{
    private const string MutexName = @"Local\JakuDownloader.SingleInstance";

    // Se mantiene mientras viva el proceso: es lo que marca que la app ya esta abierta
    private static Mutex? s_mutex;

    /// <summary>
    /// True si esta es la primera instancia. Si no y <paramref name="openExisting"/> es true, abre la ventana que ya estaba.
    /// </summary>
    public static bool TryClaim(bool openExisting)
    {
        s_mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew) return true;

        s_mutex.Dispose();
        s_mutex = null;
        if (openExisting) OpenExistingWindow();
        return false;
    }

    private static void OpenExistingWindow()
    {
        // La otra instancia puede tener la ventana oculta en segundo plano: se le pide que la muestre ella misma
        var trayWindow = NativeMethods.FindWindowW(TrayIcon.WindowClassName, null);
        if (trayWindow != 0)
        {
            // Windows solo deja pasar al frente a quien recibe permiso de la app que esta al frente, que es esta
            NativeMethods.GetWindowThreadProcessId(trayWindow, out var processId);
            NativeMethods.AllowSetForegroundWindow(processId);
            NativeMethods.PostMessageW(trayWindow, TrayIcon.OpenMessage, 0, 0);
            return;
        }

        // Recien abierta, todavia sin ventana oculta: se trae al frente la principal
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                if (process.Id == current.Id || process.MainWindowHandle == 0) continue;

                var window = process.MainWindowHandle;
                if (NativeMethods.IsIconic(window)) NativeMethods.ShowWindow(window, NativeMethods.SwRestore);
                NativeMethods.SetForegroundWindow(window);
                return;
            }
        }
    }
}
