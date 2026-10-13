using JakuDownloader.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace JakuDownloader.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = App.GetService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
    }
}
