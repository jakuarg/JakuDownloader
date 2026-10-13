using System.Security;
using JakuDownloader.Helpers;
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

    /// <summary>
    /// Muestra un toast. Al hacer clic, o en el boton "Abrir carpeta", se abre <paramref name="folder"/>.
    /// </summary>
    public void Show(string title, string message, string folder)
    {
        if (_notifier is null) return;

        // Activacion por protocolo: Windows abre la carpeta sin volver a lanzar la app
        var folderUri = Escape(new Uri(folder).AbsoluteUri);
        var xml = $"""
            <toast activationType="protocol" launch="{folderUri}">
              <visual>
                <binding template="ToastGeneric">
                  <text>{Escape(title)}</text>
                  <text>{Escape(message)}</text>
                </binding>
              </visual>
              <actions>
                <action content="Abrir carpeta" activationType="protocol" arguments="{folderUri}" />
              </actions>
            </toast>
            """;

        try
        {
            var document = new XmlDocument();
            document.LoadXml(xml);
            _notifier.Show(new ToastNotification(document));
        }
        catch (Exception)
        {
            // Un toast que falla no tiene que cortar la descarga
        }
    }

    private static string Escape(string text) => SecurityElement.Escape(text) ?? string.Empty;
}
