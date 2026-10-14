using CommunityToolkit.Mvvm.ComponentModel;

namespace JakuDownloader.ViewModels;

/// <summary>
/// Una categoria de SponsorBlock con su casilla.
/// </summary>
public sealed partial class SponsorBlockCategoryViewModel(string id, string label, bool isSelected, Action onSelectionChanged) : ObservableObject
{
    public string Id { get; } = id;

    public string Label { get; } = label;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;

    partial void OnIsSelectedChanged(bool value) => onSelectionChanged();
}
