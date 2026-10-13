using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JakuDownloader.Views.Controls;

/// <summary>
/// Distintivo "Proximamente" para las funciones que todavia no estan hechas.
/// Se oculta solo cuando <see cref="IsAvailable"/> es true.
/// </summary>
public sealed partial class FeatureBadge : UserControl
{
    public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(
        nameof(Version), typeof(string), typeof(FeatureBadge), new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty IsAvailableProperty = DependencyProperty.Register(
        nameof(IsAvailable), typeof(bool), typeof(FeatureBadge), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(FeatureBadge), new PropertyMetadata(false, OnChanged));

    public FeatureBadge()
    {
        InitializeComponent();
        Update();
    }

    public string Version
    {
        get => (string)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public bool IsAvailable
    {
        get => (bool)GetValue(IsAvailableProperty);
        set => SetValue(IsAvailableProperty, value);
    }

    /// <summary>
    /// Version corta ("Pronto") para lugares angostos como el menu lateral. La version queda en el tooltip.
    /// </summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FeatureBadge)d).Update();

    private void Update()
    {
        Label.Text = IsCompact ? "Pronto" : $"Próximamente · {Version}";
        ToolTipService.SetToolTip(this, $"Llega en la versión {Version}");
        Visibility = IsAvailable ? Visibility.Collapsed : Visibility.Visible;
    }
}
