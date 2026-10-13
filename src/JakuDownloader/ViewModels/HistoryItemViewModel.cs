using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.ViewModels;

public sealed partial class HistoryItemViewModel : ObservableObject
{
    private readonly HistoryViewModel _history;

    public HistoryItemViewModel(HistoryViewModel history, HistoryEntry entry)
    {
        _history = history;
        Entry = entry;
        FileExists = File.Exists(entry.FilePath);
    }

    public HistoryEntry Entry { get; }

    public string Title => Entry.Title;

    public string? ThumbnailUrl => Entry.ThumbnailUrl;

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (Entry.Channel is not null) parts.Add(Entry.Channel);
            parts.Add(Entry.CompletedAt.LocalDateTime.ToString("g"));
            parts.Add(Entry.Options.Label);
            if (Entry.SizeBytes is long size) parts.Add(Formatters.FormatBytes(size));
            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// Si el archivo se borro o se movio, se avisa y no se ofrece abrirlo.
    /// </summary>
    public bool FileExists { get; }

    public bool FileMissing => !FileExists;

    [RelayCommand]
    private void Open() => _history.Open(this);

    [RelayCommand]
    private void ShowInFolder() => _history.ShowInFolder(this);

    [RelayCommand]
    private void Redownload() => _history.Redownload(this);

    [RelayCommand]
    private void Remove() => _history.Remove(this);
}
