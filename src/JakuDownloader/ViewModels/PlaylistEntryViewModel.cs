using CommunityToolkit.Mvvm.ComponentModel;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.ViewModels;

/// <summary>
/// Un video de una lista, con su casilla para elegir si se baja.
/// </summary>
public sealed partial class PlaylistEntryViewModel(PlaylistEntry entry, Action onSelectionChanged) : ObservableObject
{
    public PlaylistEntry Entry { get; } = entry;

    public string Title => Entry.Title;

    public string? ThumbnailUrl => Entry.ThumbnailUrl;

    public string DurationText => Entry.DurationSeconds is int seconds ? Formatters.FormatDuration(seconds) : string.Empty;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    partial void OnIsSelectedChanged(bool value) => onSelectionChanged();
}
