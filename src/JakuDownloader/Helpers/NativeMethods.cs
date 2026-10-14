using System.Runtime.InteropServices;

namespace JakuDownloader.Helpers;

internal static partial class NativeMethods
{
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint hwnd);

    internal const int SwRestore = 9;

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SetCurrentProcessExplicitAppUserModelID(string appId);

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid folderId, uint flags, nint token, out nint path);

    /// <summary>
    /// Devuelve la carpeta Descargas real del usuario, aunque la haya movido a otro disco.
    /// </summary>
    internal static string GetDownloadsFolder()
    {
        var result = SHGetKnownFolderPath(DownloadsFolderId, 0, 0, out var pathPointer);
        try
        {
            if (result == 0)
            {
                return Marshal.PtrToStringUni(pathPointer)!;
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPointer);
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}
