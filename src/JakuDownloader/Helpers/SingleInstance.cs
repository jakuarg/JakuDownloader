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
    /// True si esta es la primera instancia. Si no, trae al frente la ventana que ya estaba abierta.
    /// </summary>
    public static bool TryClaim()
    {
        s_mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew) return true;

        s_mutex.Dispose();
        s_mutex = null;
        ActivateExistingWindow();
        return false;
    }

    private static void ActivateExistingWindow()
    {
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
