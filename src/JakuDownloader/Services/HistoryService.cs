using System.Text.Json;
using JakuDownloader.Helpers;
using JakuDownloader.Models;

namespace JakuDownloader.Services;

/// <summary>
/// Historial de descargas terminadas en %LOCALAPPDATA%\Jaku Downloader\history.json.
/// </summary>
public sealed class HistoryService
{
    // Suficiente para el uso normal sin que el archivo crezca sin limite
    private const int MaxEntries = 1000;

    private static readonly string HistoryFile = Path.Combine(AppPaths.UserDataDirectory, "history.json");

    private readonly List<HistoryEntry> _entries;

    public HistoryService()
    {
        _entries = Load();
    }

    public event EventHandler? Changed;

    /// <summary>
    /// Entradas de la mas reciente a la mas vieja.
    /// </summary>
    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public void Add(HistoryEntry entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        SaveAndNotify();
    }

    public void Remove(Guid id)
    {
        if (_entries.RemoveAll(entry => entry.Id == id) > 0) SaveAndNotify();
    }

    public void Clear()
    {
        _entries.Clear();
        SaveAndNotify();
    }

    private static List<HistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return [];
            return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(HistoryFile)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Un historial roto no tiene que impedir que la app arranque
            return [];
        }
    }

    private void SaveAndNotify()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.UserDataDirectory);
            var temp = HistoryFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_entries));
            File.Move(temp, HistoryFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si no se puede guardar, el historial sigue valiendo para esta sesion
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
