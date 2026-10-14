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

    // Segundo plano: ventana oculta, icono junto al reloj y su menu

    internal const uint WmNull = 0x0000;
    internal const uint WmContextMenu = 0x007B;
    internal const uint WmApp = 0x8000;

    internal const uint NimAdd = 0;
    internal const uint NimModify = 1;
    internal const uint NimDelete = 2;
    internal const uint NimSetVersion = 4;

    internal const uint NifMessage = 0x01;
    internal const uint NifIcon = 0x02;
    internal const uint NifTip = 0x04;
    internal const uint NifShowTip = 0x80;

    internal const uint NotifyIconVersion4 = 4;

    // Con NOTIFYICON_VERSION_4: clic izquierdo y Enter/espacio sobre el icono
    internal const uint NinSelect = 0x0400;
    internal const uint NinKeySelect = 0x0401;

    internal const uint MfString = 0x0000;
    internal const uint MfGrayed = 0x0001;
    internal const uint MfSeparator = 0x0800;

    internal const uint TpmRightAlign = 0x0008;
    internal const uint TpmBottomAlign = 0x0020;
    internal const uint TpmNoNotify = 0x0080;
    internal const uint TpmReturnCmd = 0x0100;
    internal const uint TpmRightButton = 0x0002;

    internal const int SmCxSmallIcon = 49;
    internal const int SmCySmallIcon = 50;
    internal const int SmMenuDropAlignment = 40;

    internal const uint ImageIcon = 1;
    internal const uint LrLoadFromFile = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowClass
    {
        public uint Size;
        public uint Style;
        public nint WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint SmallIcon;
    }

    /// <summary>
    /// NOTIFYICONDATAW completa (incluye los campos de globos aunque no se usen: Windows valida el tamano).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;
        public fixed char ToolTip[128];
        public uint State;
        public uint StateMask;
        public fixed char Info[256];
        public uint Version;
        public fixed char InfoTitle[64];
        public uint InfoFlags;
        public Guid Item;
        public nint BalloonIcon;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial ushort RegisterClassExW(in WindowClass windowClass);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowExW(
        uint extendedStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial nint DefWindowProcW(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessageW(string message);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindowW(string className, string? windowName);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AllowSetForegroundWindow(uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessageW(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandleW(string? moduleName);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Shell_NotifyIconW(uint message, in NotifyIconData data);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadImageW(nint instance, string name, uint type, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    internal static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AppendMenuW(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetMenuDefaultItem(nint menu, uint item, uint byPosition);

    [LibraryImport("user32.dll")]
    internal static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyMenu(nint menu);

    /// <summary>
    /// SetPreferredAppMode de uxtheme, sin documentar pero estable desde Windows 10 1903. Con 1 (AllowDark)
    /// los menus de Win32 siguen el tema oscuro del sistema; sin esto el menu del icono sale siempre claro.
    /// </summary>
    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    internal static partial int SetPreferredAppMode(int mode);

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
