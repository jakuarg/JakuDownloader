using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace JakuDownloader.Helpers;

/// <summary>
/// Icono en el area de notificacion, junto al reloj. WinUI 3 no trae uno, asi que se usa la API de Win32.
/// </summary>
/// <remarks>
/// La ventana oculta que recibe los clics existe siempre, aunque el icono no se vea: otra instancia de la app
/// la busca por su clase para pedir que se abra la ventana principal (ver <see cref="SingleInstance"/>).
/// </remarks>
public sealed unsafe class TrayIcon : IDisposable
{
    public const string WindowClassName = "JakuDownloader.TrayWindow";

    /// <summary>
    /// Mensaje que otra instancia le manda a la ventana oculta para que se abra la ventana principal.
    /// </summary>
    public const uint OpenMessage = NativeMethods.WmApp + 2;

    private const uint CallbackMessage = NativeMethods.WmApp + 1;
    private const uint IconId = 1;

    // Una sola por proceso: el procedimiento de ventana es estatico y llega hasta aca por este campo
    private static TrayIcon? s_current;

    private readonly Func<IReadOnlyList<TrayMenuItem>> _menuItems;
    private readonly DispatcherQueue _dispatcher;
    private readonly uint _taskbarCreatedMessage;
    private readonly nint _window;
    private readonly nint _icon;
    private string _toolTip = string.Empty;
    private bool _isVisible;

    /// <summary>
    /// Se crea en el hilo de la interfaz: los mensajes de la ventana oculta llegan por ese mismo hilo.
    /// </summary>
    public TrayIcon(string iconPath, Func<IReadOnlyList<TrayMenuItem>> menuItems)
    {
        if (s_current is not null) throw new InvalidOperationException("Ya hay un icono creado.");

        _menuItems = menuItems;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Explorer manda este mensaje cuando se reinicia (o cambia el DPI de la barra de tareas): hay que volver a agregar el icono
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");

        try
        {
            NativeMethods.SetPreferredAppMode(1);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            // Sin esto el menu sale claro, nada mas
        }

        var instance = NativeMethods.GetModuleHandleW(null);
        fixed (char* className = WindowClassName)
        {
            var windowClass = new NativeMethods.WindowClass
            {
                Size = (uint)sizeof(NativeMethods.WindowClass),
                WindowProcedure = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WindowProcedure,
                Instance = instance,
                ClassName = (nint)className,
            };
            NativeMethods.RegisterClassExW(windowClass);
        }

        s_current = this;

        // Ventana comun (no "message-only") porque esas no reciben TaskbarCreated. Nunca se muestra.
        _window = NativeMethods.CreateWindowExW(0, WindowClassName, "Jaku Downloader", 0, 0, 0, 0, 0, 0, 0, instance, 0);
        if (_window == 0)
        {
            s_current = null;
            throw new InvalidOperationException($"No se pudo crear la ventana del icono (error {Marshal.GetLastPInvokeError()}).");
        }

        _icon = NativeMethods.LoadImageW(0, iconPath, NativeMethods.ImageIcon,
            NativeMethods.GetSystemMetrics(NativeMethods.SmCxSmallIcon), NativeMethods.GetSystemMetrics(NativeMethods.SmCySmallIcon),
            NativeMethods.LrLoadFromFile);
    }

    /// <summary>
    /// Clic o Enter sobre el icono, o pedido de otra instancia de la app.
    /// </summary>
    public event EventHandler? OpenRequested;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value == _isVisible) return;
            _isVisible = value;
            if (value) Add();
            else Remove();
        }
    }

    /// <summary>
    /// Texto al pasar el mouse. Windows admite hasta 127 caracteres.
    /// </summary>
    public string ToolTip
    {
        get => _toolTip;
        set
        {
            _toolTip = value.Length > 127 ? value[..127] : value;
            if (_isVisible) Update(NativeMethods.NimModify);
        }
    }

    public void Dispose()
    {
        if (s_current != this) return;

        Remove();
        _isVisible = false;
        NativeMethods.DestroyWindow(_window);
        if (_icon != 0) NativeMethods.DestroyIcon(_icon);
        s_current = null;
    }

    private void Add()
    {
        Update(NativeMethods.NimAdd);

        // La version 4 avisa los clics con NIN_SELECT y manda las coordenadas del menu, tambien desde el teclado
        var data = CreateData();
        data.Version = NativeMethods.NotifyIconVersion4;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NimSetVersion, data);
    }

    private void Remove()
    {
        var data = CreateData();
        NativeMethods.Shell_NotifyIconW(NativeMethods.NimDelete, data);
    }

    private void Update(uint message)
    {
        var data = CreateData();
        data.Flags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip | NativeMethods.NifShowTip;
        data.CallbackMessage = CallbackMessage;
        data.Icon = _icon;
        fixed (char* toolTip = _toolTip)
        {
            Buffer.MemoryCopy(toolTip, data.ToolTip, 128 * sizeof(char), _toolTip.Length * sizeof(char));
        }

        NativeMethods.Shell_NotifyIconW(message, data);
    }

    private NativeMethods.NotifyIconData CreateData() => new()
    {
        Size = (uint)sizeof(NativeMethods.NotifyIconData),
        Window = _window,
        Id = IconId,
    };

    private void ShowMenu(int x, int y)
    {
        var items = _menuItems();
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.IsSeparator)
                {
                    NativeMethods.AppendMenuW(menu, NativeMethods.MfSeparator, 0, null);
                    continue;
                }

                // El id es la posicion mas uno: TrackPopupMenuEx devuelve 0 cuando se cierra sin elegir nada
                var flags = NativeMethods.MfString | (item.IsEnabled ? 0 : NativeMethods.MfGrayed);
                NativeMethods.AppendMenuW(menu, flags, (nuint)(i + 1), item.Text);
                if (item.IsDefault) NativeMethods.SetMenuDefaultItem(menu, (uint)(i + 1), 0);
            }

            // Sin pasar al frente la ventana oculta, el menu no se cierra al hacer clic afuera
            NativeMethods.SetForegroundWindow(_window);

            var alignment = NativeMethods.GetSystemMetrics(NativeMethods.SmMenuDropAlignment) != 0 ? NativeMethods.TpmRightAlign : 0;
            var command = NativeMethods.TrackPopupMenuEx(menu,
                NativeMethods.TpmReturnCmd | NativeMethods.TpmNoNotify | NativeMethods.TpmRightButton | NativeMethods.TpmBottomAlign | alignment,
                x, y, _window, 0);

            // Recomendacion de la documentacion de TrackPopupMenu para que el segundo clic sobre el icono funcione
            NativeMethods.PostMessageW(_window, NativeMethods.WmNull, 0, 0);

            if (command > 0 && items[command - 1] is { Action: { } action, IsEnabled: true })
            {
                // Fuera del procedimiento de ventana, para que una excepcion siga el camino normal de la app
                _dispatcher.TryEnqueue(() => action());
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private void RaiseOpenRequested() => _dispatcher.TryEnqueue(() => OpenRequested?.Invoke(this, EventArgs.Empty));

    private bool HandleMessage(uint message, nint wParam, nint lParam)
    {
        if (message == OpenMessage)
        {
            RaiseOpenRequested();
            return true;
        }

        if (message == _taskbarCreatedMessage)
        {
            if (_isVisible) Add();
            return false;
        }

        if (message != CallbackMessage) return false;

        // Version 4: el evento va en la parte baja de lParam y las coordenadas en wParam
        switch ((uint)(lParam & 0xFFFF))
        {
            case NativeMethods.NinSelect:
            case NativeMethods.NinKeySelect:
                RaiseOpenRequested();
                return true;

            case NativeMethods.WmContextMenu:
                ShowMenu((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));
                return true;

            default:
                return false;
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        // Una excepcion que cruce a Win32 cierra el proceso sin aviso
        try
        {
            if (s_current is { } tray && tray._window == hwnd && tray.HandleMessage(message, wParam, lParam)) return 0;
        }
        catch (Exception)
        {
        }

        return NativeMethods.DefWindowProcW(hwnd, message, wParam, lParam);
    }
}
