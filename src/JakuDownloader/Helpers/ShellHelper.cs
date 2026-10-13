using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace JakuDownloader.Helpers;

/// <summary>
/// Abre carpetas en el Explorador. Los fallos se devuelven como mensaje para mostrar, no como excepcion.
/// </summary>
public static class ShellHelper
{
    public static bool TryOpenFolder(string folder, [NotNullWhen(false)] out string? error)
    {
        return TryRun(() =>
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }, out error);
    }

    /// <summary>
    /// Abre el Explorador con el archivo seleccionado. Si ya no existe, abre su carpeta.
    /// </summary>
    public static bool TryShowInFolder(string filePath, [NotNullWhen(false)] out string? error)
    {
        if (!File.Exists(filePath))
        {
            return TryOpenFolder(Path.GetDirectoryName(filePath)!, out error);
        }

        // explorer necesita la ruta entre comillas pegada a /select,
        return TryRun(() => Process.Start("explorer.exe", $"/select,\"{filePath}\""), out error);
    }

    private static bool TryRun(Action action, [NotNullWhen(false)] out string? error)
    {
        try
        {
            action();
            error = null;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            error = "No hay permiso para acceder a la carpeta.";
        }
        catch (IOException)
        {
            error = "La carpeta no está disponible. Comprobá que la unidad esté conectada.";
        }
        catch (Win32Exception)
        {
            error = "Windows no pudo abrir el Explorador.";
        }

        return false;
    }
}
