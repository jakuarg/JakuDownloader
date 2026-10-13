using JakuDownloader.Helpers;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace JakuDownloader.Services;

/// <summary>
/// Muestra toasts de Windows. Si el sistema no los soporta, no hace nada.
/// </summary>
/// <remarks>
/// No usa AppNotificationManager: en apps sin empaquetar registra un AppUserModelID propio
/// sin acceso directo en Inicio, y Windows entrega esos toasts pero no los muestra.
/// </remarks>
public sealed class NotificationService
{
    private ToastNotifier? _notifier;

    public void Initialize()
    {
        try
        {
            StartMenuShortcut.Ensure(AppIdentity.AppUserModelId);
            _notifier = ToastNotificationManager.CreateToastNotifier(AppIdentity.AppUserModelId);
        }
        catch (Exception)
        {
            // Sin toasts la app sigue funcionando
        }
    }

    public void Show(string title, string message)
    {
        if (_notifier is null) return;

        // El builder arma el XML y se encarga de escapar el texto
        var payload = new AppNotificationBuilder()
            .AddText(title)
            .AddText(message)
            .BuildNotification()
            .Payload;

        var xml = new XmlDocument();
        xml.LoadXml(payload);
        _notifier.Show(new ToastNotification(xml));
    }
}
