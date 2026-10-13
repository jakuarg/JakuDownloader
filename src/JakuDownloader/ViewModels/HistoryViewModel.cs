using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JakuDownloader.Helpers;
using JakuDownloader.Models;
using JakuDownloader.Services;

namespace JakuDownloader.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryService _historyService;
    private readonly DownloadQueueViewModel _queue;
    private readonly SettingsService _settings;

    public HistoryViewModel(HistoryService historyService, DownloadQueueViewModel queue, SettingsService settings)
    {
        _historyService = historyService;
        _queue = queue;
        _settings = settings;
        historyService.Changed += (_, _) => Rebuild();
        Rebuild();
    }

    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsMessageOpen { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public bool HasEntries => _historyService.Entries.Count > 0;

    public bool IsEmpty => Items.Count == 0;

    public string EmptyText => HasEntries
        ? "Ninguna descarga coincide con la búsqueda."
        : "Todavía no hay descargas terminadas. Cuando bajes algo, va a aparecer acá.";

    public void Clear() => _historyService.Clear();

    internal void Open(HistoryItemViewModel item)
    {
        if (!ShellHelper.TryOpenFile(item.Entry.FilePath, out var error)) ShowMessage(error);
    }

    internal void ShowInFolder(HistoryItemViewModel item)
    {
        if (!ShellHelper.TryShowInFolder(item.Entry.FilePath, out var error)) ShowMessage(error);
    }

    internal void Redownload(HistoryItemViewModel item)
    {
        var entry = item.Entry;
        if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var url)) return;

        var preview = new VideoInfo(string.Empty, entry.Title, entry.Channel, null, entry.ThumbnailUrl, false, false);
        _queue.Enqueue(url, entry.Options, _settings.DownloadDirectory, preview);
        ShowMessage($"Se agregó a la cola: {entry.Title}");
    }

    internal void Remove(HistoryItemViewModel item) => _historyService.Remove(item.Entry.Id);

    partial void OnSearchTextChanged(string value) => Rebuild();

    private void Rebuild()
    {
        var query = SearchText.Trim();
        var matches = _historyService.Entries.Where(entry => query.Length == 0
            || entry.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || (entry.Channel?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false));

        Items.Clear();
        foreach (var entry in matches) Items.Add(new HistoryItemViewModel(this, entry));

        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void ShowMessage(string message)
    {
        Message = message;
        IsMessageOpen = true;
    }
}
