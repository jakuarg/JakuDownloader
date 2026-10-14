using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JakuDownloader.Helpers;
using JakuDownloader.Services;

namespace JakuDownloader.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly PickerService _picker;

    // Mientras se cargan los valores guardados, los cambios de propiedades no se vuelven a guardar
    private readonly bool _loading;

    public SettingsViewModel(SettingsService settings, PickerService picker, ToolsViewModel tools)
    {
        _settings = settings;
        _picker = picker;
        Tools = tools;

        _loading = true;
        DownloadDirectory = settings.DownloadDirectory;
        TemplateText = settings.FileNameTemplate;
        MaxConcurrentDownloads = settings.MaxConcurrentDownloads;

        var network = settings.Current;
        CookiesBrowserIndex = NetworkOptions.Browsers.Select(b => b.Id).ToList().IndexOf(network.CookiesBrowser ?? string.Empty) + 1;
        CookiesFile = network.CookiesFile;
        ProxyText = network.Proxy ?? string.Empty;
        var (rate, megabytes) = NetworkOptions.ParseRateLimit(network.RateLimit);
        IsRateLimitEnabled = network.RateLimit is not null;
        RateLimitValue = rate;
        RateLimitUnitIndex = megabytes ? 1 : 0;
        _loading = false;
    }

    // Red: cookies

    public IReadOnlyList<string> CookiesBrowserOptions { get; } = ["No usar", .. NetworkOptions.Browsers.Select(b => b.Label)];

    /// <summary>
    /// 0: no usar; 1 en adelante: el navegador de <see cref="NetworkOptions.Browsers"/> en esa posicion menos uno.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CookiesDescription))]
    public partial int CookiesBrowserIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCookiesFile))]
    [NotifyPropertyChangedFor(nameof(CookiesFileText))]
    [NotifyPropertyChangedFor(nameof(CookiesDescription))]
    public partial string? CookiesFile { get; set; }

    public bool HasCookiesFile => CookiesFile is not null;

    public string CookiesFileText => CookiesFile ?? "Ninguno";

    public string CookiesDescription => (CookiesFile, CookiesBrowserIndex) switch
    {
        ({ }, _) => "Se usa el archivo de cookies elegido",
        (_, 0) => "Para videos con restricción de edad o que piden iniciar sesión",
        (_, 1) => "Usa la sesión de YouTube que tengas abierta en Firefox",
        _ => "Chrome, Edge y los basados en Chromium cifran las cookies y a veces no se pueden leer. Cerrá el navegador antes de descargar; si falla, probá con Firefox o con un archivo cookies.txt",
    };

    partial void OnCookiesBrowserIndexChanged(int value)
    {
        // -1: el ComboBox se quedo un momento sin seleccion; no es una eleccion del usuario y no tiene que borrar nada
        if (_loading || value < 0) return;

        var browser = value >= 1 && value <= NetworkOptions.Browsers.Count ? NetworkOptions.Browsers[value - 1].Id : null;
        if (browser == _settings.Current.CookiesBrowser) return;

        // Navegador y archivo se excluyen: elegir uno descarta el otro
        if (TrySave(current => current with { CookiesBrowser = browser, CookiesFile = browser is null ? current.CookiesFile : null }))
        {
            CookiesFile = _settings.Current.CookiesFile;
        }
    }

    [RelayCommand]
    private async Task ChooseCookiesFileAsync()
    {
        IsErrorOpen = false;
        string? path;
        try
        {
            path = await _picker.PickFileAsync(".txt");
        }
        catch (Exception)
        {
            ShowError("No se pudo abrir el selector de archivos", "Volvé a intentar.");
            return;
        }

        if (path is null) return;

        if (!NetworkOptions.LooksLikeCookiesFile(path))
        {
            ShowError("Ese archivo no parece un cookies.txt",
                "yt-dlp solo acepta cookies en formato Netscape. Exportalas con una extensión del navegador como \"Get cookies.txt LOCALLY\".");
            return;
        }

        if (TrySave(current => current with { CookiesFile = path, CookiesBrowser = null }))
        {
            CookiesFile = path;
            CookiesBrowserIndex = 0;
        }
    }

    [RelayCommand]
    private void ClearCookiesFile()
    {
        if (TrySave(current => current with { CookiesFile = null })) CookiesFile = null;
    }

    // Red: proxy

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProxyError))]
    [NotifyPropertyChangedFor(nameof(HasProxyError))]
    [NotifyCanExecuteChangedFor(nameof(SaveProxyCommand))]
    public partial string ProxyText { get; set; }

    public string ProxyError => ProxyText.Trim().Length == 0 ? string.Empty : NetworkOptions.ValidateProxy(ProxyText) ?? string.Empty;

    public bool HasProxyError => ProxyError.Length > 0;

    public string ProxyDescription => _settings.Current.Proxy is { } proxy
        ? $"En uso: {proxy}"
        : "Solo el tráfico de la app pasa por el proxy (HTTP o SOCKS)";

    public bool HasProxy => _settings.Current.Proxy is not null;

    private bool CanSaveProxy() => ProxyText.Trim().Length > 0 && !HasProxyError && ProxyText.Trim() != _settings.Current.Proxy;

    [RelayCommand(CanExecute = nameof(CanSaveProxy))]
    private void SaveProxy() => SetProxy(ProxyText.Trim());

    [RelayCommand]
    private void ClearProxy()
    {
        ProxyText = string.Empty;
        SetProxy(null);
    }

    private void SetProxy(string? proxy)
    {
        IsErrorOpen = false;
        if (!TrySave(current => current with { Proxy = proxy })) return;
        OnPropertyChanged(nameof(ProxyDescription));
        OnPropertyChanged(nameof(HasProxy));
        SaveProxyCommand.NotifyCanExecuteChanged();
    }

    // Red: limite de velocidad

    [ObservableProperty]
    public partial bool IsRateLimitEnabled { get; set; }

    [ObservableProperty]
    public partial double RateLimitValue { get; set; }

    /// <summary>
    /// 0: KB/s, 1: MB/s.
    /// </summary>
    [ObservableProperty]
    public partial int RateLimitUnitIndex { get; set; }

    partial void OnIsRateLimitEnabledChanged(bool value) => SaveRateLimit();

    partial void OnRateLimitValueChanged(double value) => SaveRateLimit();

    partial void OnRateLimitUnitIndexChanged(int value) => SaveRateLimit();

    private void SaveRateLimit()
    {
        if (_loading || RateLimitUnitIndex < 0) return;

        var rateLimit = IsRateLimitEnabled ? NetworkOptions.FormatRateLimit(RateLimitValue, RateLimitUnitIndex == 1) : null;

        // NumberBox vacio (NaN): se espera a que escriban un numero
        if (IsRateLimitEnabled && rateLimit is null) return;
        if (rateLimit == _settings.Current.RateLimit) return;

        TrySave(current => current with { RateLimit = rateLimit });
    }

    /// <summary>
    /// Double porque es lo que usa NumberBox; se guarda como entero entre 1 y 10.
    /// </summary>
    [ObservableProperty]
    public partial double MaxConcurrentDownloads { get; set; }

    partial void OnMaxConcurrentDownloadsChanged(double value)
    {
        // NumberBox informa NaN si se borra el numero: se espera a que escriban uno
        if (_loading || double.IsNaN(value)) return;

        var limit = Math.Clamp((int)Math.Round(value), 1, 10);
        if (limit == _settings.MaxConcurrentDownloads) return;

        var stored = limit == SettingsService.DefaultMaxConcurrentDownloads ? (int?)null : limit;
        TrySave(current => current with { MaxConcurrentDownloads = stored });
    }

    public ToolsViewModel Tools { get; }

    public string AppVersion => AppIdentity.DisplayVersion;

    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    [ObservableProperty]
    public partial string ErrorTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    // Carpeta de destino

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomDirectory))]
    public partial string DownloadDirectory { get; set; }

    public bool IsCustomDirectory => _settings.Current.DownloadDirectory is not null;

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        IsErrorOpen = false;
        if (!ShellHelper.TryOpenFolder(DownloadDirectory, out var error)) ShowError("No se pudo abrir la carpeta", error);
    }

    [RelayCommand]
    private async Task ChangeDownloadFolderAsync()
    {
        IsErrorOpen = false;
        string? folder;
        try
        {
            folder = await _picker.PickFolderAsync();
        }
        catch (Exception)
        {
            ShowError("No se pudo abrir el selector de carpetas", "Volvé a intentar.");
            return;
        }

        if (folder is null) return;

        if (!AppPaths.IsWritable(folder))
        {
            ShowError("No se puede usar esa carpeta", $"No hay permiso para guardar archivos en {folder}. Elegí otra.");
            return;
        }

        // Elegir la carpeta por defecto equivale a no tener una personalizada
        var isDefault = string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), Path.GetFullPath(AppPaths.DefaultDownloadDirectory).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
        SaveDownloadDirectory(isDefault ? null : folder);
    }

    [RelayCommand]
    private void ResetDownloadFolder() => SaveDownloadDirectory(null);

    private void SaveDownloadDirectory(string? folder)
    {
        if (!TrySave(current => current with { DownloadDirectory = folder })) return;
        DownloadDirectory = _settings.DownloadDirectory;
        OnPropertyChanged(nameof(IsCustomDirectory));
    }

    // Plantilla del nombre de archivo

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemplateError))]
    [NotifyPropertyChangedFor(nameof(IsTemplateValid))]
    [NotifyPropertyChangedFor(nameof(IsTemplateInvalid))]
    [NotifyPropertyChangedFor(nameof(TemplateExample))]
    [NotifyCanExecuteChangedFor(nameof(SaveTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetTemplateCommand))]
    public partial string TemplateText { get; set; }

    public string TemplateError => FileNameTemplate.Validate(TemplateText) ?? string.Empty;

    public bool IsTemplateValid => FileNameTemplate.Validate(TemplateText) is null;

    public bool IsTemplateInvalid => !IsTemplateValid;

    public string TemplateExample => IsTemplateValid ? $"Ejemplo: {FileNameTemplate.Preview(TemplateText)}" : string.Empty;

    private bool CanSaveTemplate() => IsTemplateValid && TemplateText != _settings.FileNameTemplate;

    [RelayCommand(CanExecute = nameof(CanSaveTemplate))]
    private void SaveTemplate()
    {
        IsErrorOpen = false;
        var template = TemplateText == FileNameTemplate.Default ? null : TemplateText;
        if (TrySave(current => current with { FileNameTemplate = template }))
        {
            SaveTemplateCommand.NotifyCanExecuteChanged();
            ResetTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanResetTemplate() => TemplateText != FileNameTemplate.Default || _settings.Current.FileNameTemplate is not null;

    [RelayCommand(CanExecute = nameof(CanResetTemplate))]
    private void ResetTemplate()
    {
        TemplateText = FileNameTemplate.Default;
        if (TrySave(current => current with { FileNameTemplate = null }))
        {
            SaveTemplateCommand.NotifyCanExecuteChanged();
            ResetTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool TrySave(Func<Models.AppSettings, Models.AppSettings> change)
    {
        try
        {
            _settings.Update(change);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("No se pudo guardar la configuración", $"No se pudo escribir en {AppPaths.SettingsFile}.");
            return false;
        }
    }

    private void ShowError(string title, string message)
    {
        ErrorTitle = title;
        ErrorMessage = message;
        IsErrorOpen = true;
    }
}
