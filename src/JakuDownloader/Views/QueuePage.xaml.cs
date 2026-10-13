using JakuDownloader.ViewModels;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace JakuDownloader.Views;

public sealed partial class QueuePage : Page
{
    public QueuePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Queue.AnnouncementRequested += OnAnnouncementRequested;
        Unloaded += (_, _) => Queue.AnnouncementRequested -= OnAnnouncementRequested;
    }

    public DownloadQueueViewModel Queue { get; } = App.GetService<DownloadQueueViewModel>();

    private void OnAnnouncementRequested(object? sender, string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(ContentPanel)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(ContentPanel);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "JakuDownloader.Queue");
    }
}
