using System.ComponentModel;
using JakuDownloader.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace JakuDownloader.Views;

public sealed partial class DownloadPage : Page
{
    private bool _focusWasInUrlRow;

    public DownloadViewModel ViewModel { get; } = App.GetService<DownloadViewModel>();

    public ToolsViewModel Tools { get; } = App.GetService<ToolsViewModel>();

    public DownloadPage()
    {
        InitializeComponent();

        // La pagina queda en cache: se engancha al mostrarse y se suelta al ocultarse
        Loaded += (_, _) =>
        {
            ViewModel.PropertyChanging += OnViewModelPropertyChanging;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ViewModel.AnnouncementRequested += OnAnnouncementRequested;
        };
        Unloaded += (_, _) =>
        {
            ViewModel.PropertyChanging -= OnViewModelPropertyChanging;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.AnnouncementRequested -= OnAnnouncementRequested;
        };
    }

    private void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.DownloadCommand.CanExecute(null))
        {
            ViewModel.DownloadCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnViewModelPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        // Antes de que Descargar/Cancelar se oculten: si el foco estaba en la fila del link, se recuerda
        if (e.PropertyName == nameof(DownloadViewModel.IsDownloading))
        {
            _focusWasInUrlRow = UrlBox.FocusState != FocusState.Unfocused
                || PasteButton.FocusState != FocusState.Unfocused
                || DownloadButton.FocusState != FocusState.Unfocused
                || CancelButton.FocusState != FocusState.Unfocused;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DownloadViewModel.IsDownloading) || !_focusWasInUrlRow) return;

        // Se espera a que se apliquen los bindings (visibilidad y habilitacion) antes de mover el foco
        DispatcherQueue.TryEnqueue(() =>
        {
            Control target = ViewModel.IsDownloading ? CancelButton : UrlBox;
            target.Focus(FocusState.Programmatic);
        });
    }

    private void OnAnnouncementRequested(object? sender, string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(StageLabel)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(StageLabel);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "JakuDownloader.DownloadStage");
    }
}
