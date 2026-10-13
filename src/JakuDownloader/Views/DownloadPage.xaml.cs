using JakuDownloader.ViewModels;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace JakuDownloader.Views;

public sealed partial class DownloadPage : Page
{
    public DownloadPage()
    {
        InitializeComponent();

        // La pagina queda en cache: se engancha al mostrarse y se suelta al ocultarse
        Loaded += (_, _) =>
        {
            ViewModel.AnnouncementRequested += OnAnnouncementRequested;
            ViewModel.Queue.AnnouncementRequested += OnAnnouncementRequested;
        };
        Unloaded += (_, _) =>
        {
            ViewModel.AnnouncementRequested -= OnAnnouncementRequested;
            ViewModel.Queue.AnnouncementRequested -= OnAnnouncementRequested;
        };
    }

    public DownloadViewModel ViewModel { get; } = App.GetService<DownloadViewModel>();

    public ToolsViewModel Tools { get; } = App.GetService<ToolsViewModel>();

    private void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.DownloadCommand.CanExecute(null))
        {
            ViewModel.DownloadCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnAnnouncementRequested(object? sender, string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(UrlBox)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(UrlBox);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "JakuDownloader.Download");
    }
}
