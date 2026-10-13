using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace JakuDownloader.Helpers;

/// <summary>
/// Crea el acceso directo de la app en el menu Inicio, marcado con su AppUserModelID.
/// </summary>
/// <remarks>
/// Windows no muestra los toasts de una app de escritorio sin empaquetar
/// si no hay un acceso directo en Inicio con el mismo AppUserModelID.
/// </remarks>
internal static class StartMenuShortcut
{
    private const ushort VtLpwstr = 31;
    private static readonly Guid AppUserModelIdFormat = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private const uint AppUserModelIdProperty = 5;

    public static string ShortcutPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Jaku Downloader.lnk");

    /// <summary>
    /// Crea el acceso directo o lo actualiza si apunta a otro ejecutable (por ejemplo, si se movio la carpeta).
    /// </summary>
    public static void Ensure(string appUserModelId)
    {
        var target = Environment.ProcessPath!;
        var link = (IShellLinkW)new CShellLink();

        if (File.Exists(ShortcutPath))
        {
            ((IPersistFile)link).Load(ShortcutPath, 0);
            var currentTarget = new StringBuilder(1024);
            link.GetPath(currentTarget, currentTarget.Capacity, 0, 0);
            if (string.Equals(currentTarget.ToString(), target, StringComparison.OrdinalIgnoreCase)) return;
        }

        link.SetPath(target);
        link.SetWorkingDirectory(Path.GetDirectoryName(target)!);
        link.SetDescription("Descargador de audio y video");

        var store = (IPropertyStore)link;
        var key = new PropertyKey { FormatId = AppUserModelIdFormat, PropertyId = AppUserModelIdProperty };
        var value = new PropVariant { VarType = VtLpwstr, Pointer = Marshal.StringToCoTaskMemUni(appUserModelId) };
        try
        {
            store.SetValue(ref key, ref value);
            store.Commit();
        }
        finally
        {
            Marshal.FreeCoTaskMem(value.Pointer);
        }

        ((IPersistFile)link).Save(ShortcutPath, true);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink;

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, nint findData, uint flags);
        void GetIDList(out nint idList);
        void SetIDList(nint idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        void Resolve(nint hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    // Solo se usa con cadenas (VT_LPWSTR); el tamano cubre el PROPVARIANT completo en x64
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort VarType;
        [FieldOffset(8)] public nint Pointer;
    }
}
