using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Helpers;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed record ThemeOption(AppThemeMode Value, string Label)
{
    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new(AppThemeMode.Dark, "Sombre"),
        new(AppThemeMode.Light, "Clair"),
        new(AppThemeMode.System, "Système"),
    ];

    public static ThemeOption From(AppThemeMode value) => All.First(o => o.Value == value);
}

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IToolchainService _toolchain;
    private readonly IUpdateService _updates;
    private bool _suppressPersist;

    [ObservableProperty]
    private string _downloadDirectory = string.Empty;

    [ObservableProperty]
    private AudioFormatOption _selectedFormat;

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    [ObservableProperty]
    private int _maxParallelDownloads;

    [ObservableProperty]
    private int _rateLimitKbps;

    [ObservableProperty]
    private bool _autoPasteClipboardLinks;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup;

    [ObservableProperty]
    private string _toolStatusText = "Vérification des composants…";

    [ObservableProperty]
    private bool _toolsBusy;

    [ObservableProperty]
    private string? _updateStatusText;

    public SettingsViewModel(ISettingsService settings, IToolchainService toolchain, IUpdateService updates)
    {
        _settings = settings;
        _toolchain = toolchain;
        _updates = updates;

        var current = settings.Current;
        _suppressPersist = true;
        _downloadDirectory = string.IsNullOrWhiteSpace(current.DownloadDirectory)
            ? settings.DefaultDownloadDirectory
            : current.DownloadDirectory;
        _selectedFormat = AudioFormatOption.From(current.PreferredFormat);
        _selectedTheme = ThemeOption.From(current.Theme);
        _maxParallelDownloads = current.MaxParallelDownloads;
        _rateLimitKbps = current.RateLimitKbps;
        _autoPasteClipboardLinks = current.AutoPasteClipboardLinks;
        _showNotifications = current.ShowNotifications;
        _checkForUpdatesOnStartup = current.CheckForUpdatesOnStartup;
        _suppressPersist = false;
    }

    public IReadOnlyList<AudioFormatOption> Formats => AudioFormatOption.All;

    public IReadOnlyList<ThemeOption> Themes => ThemeOption.All;

    public string AppVersion => $"WaveDL {AppInfo.VersionText}";

    public string RateLimitDisplay => RateLimitKbps <= 0 ? "Illimité" : $"{RateLimitKbps} Ko/s";

    public async Task LoadAsync() => await RefreshToolStatusAsync().ConfigureAwait(true);

    partial void OnSelectedFormatChanged(AudioFormatOption value) =>
        Persist(s => s.PreferredFormat = value.Value);

    partial void OnMaxParallelDownloadsChanged(int value) =>
        Persist(s => s.MaxParallelDownloads = Math.Clamp(value, 1, 8));

    partial void OnRateLimitKbpsChanged(int value)
    {
        OnPropertyChanged(nameof(RateLimitDisplay));
        Persist(s => s.RateLimitKbps = Math.Max(0, value));
    }

    partial void OnAutoPasteClipboardLinksChanged(bool value) =>
        Persist(s => s.AutoPasteClipboardLinks = value);

    partial void OnShowNotificationsChanged(bool value) =>
        Persist(s => s.ShowNotifications = value);

    partial void OnCheckForUpdatesOnStartupChanged(bool value) =>
        Persist(s => s.CheckForUpdatesOnStartup = value);

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        Persist(s => s.Theme = value.Value);
        UiInterop.ApplyTheme(value.Value);
    }

    [RelayCommand]
    private async Task ChangeFolderAsync()
    {
        var folder = await UiInterop.PickFolderAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DownloadDirectory = folder;
            Persist(s => s.DownloadDirectory = folder);
        }
    }

    [RelayCommand]
    private void OpenDownloadFolder() => SystemLauncher.RevealInExplorer(_settings.EffectiveDownloadDirectory);

    [RelayCommand]
    private void OpenLogsFolder() => SystemLauncher.RevealInExplorer(_settings.LogDirectory);

    [RelayCommand]
    private async Task InstallToolsAsync()
    {
        if (ToolsBusy)
        {
            return;
        }

        ToolsBusy = true;
        try
        {
            var progress = new Progress<string>(message => RunOnUi(() => ToolStatusText = message));
            await _toolchain.EnsureReadyAsync(progress).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ToolStatusText = $"Échec : {ex.Message}";
        }
        finally
        {
            ToolsBusy = false;
            await RefreshToolStatusAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task UpdateYtDlpAsync()
    {
        if (ToolsBusy)
        {
            return;
        }

        ToolsBusy = true;
        try
        {
            var progress = new Progress<string>(message => RunOnUi(() => ToolStatusText = message));
            await _toolchain.UpdateYtDlpAsync(progress).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ToolStatusText = $"Échec : {ex.Message}";
        }
        finally
        {
            ToolsBusy = false;
            await RefreshToolStatusAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        UpdateStatusText = "Vérification…";
        var result = await _updates.CheckAsync().ConfigureAwait(true);
        UpdateStatusText = result switch
        {
            { UpdateAvailable: true, LatestVersion: { } v } => $"Version {v} disponible.",
            { LatestVersion: { } v } => $"À jour (dernière version publiée : {v}).",
            _ => "Aucune information de version disponible.",
        };
    }

    private async Task RefreshToolStatusAsync()
    {
        var status = await _toolchain.RefreshAsync().ConfigureAwait(true);
        ToolStatusText = status switch
        {
            { IsReady: true } => $"yt-dlp {status.YtDlpVersion ?? "?"} · FFmpeg détecté.",
            { YtDlpInstalled: true } => "yt-dlp détecté, FFmpeg manquant.",
            { FfmpegInstalled: true } => "FFmpeg détecté, yt-dlp manquant.",
            _ => "yt-dlp et FFmpeg manquants — installez-les ci-dessous.",
        };
    }

    private void Persist(Action<AppSettingsModel> apply)
    {
        if (_suppressPersist)
        {
            return;
        }

        apply(_settings.Current);
        _ = _settings.SaveAsync();
    }
}
