namespace WaveDL.Models;

/// <summary>Serialized to <c>%LOCALAPPDATA%\WaveDL\settings.json</c>.</summary>
public sealed class AppSettingsModel
{
    /// <summary>Empty → resolved to <c>Music\WaveDL</c> at runtime.</summary>
    public string DownloadDirectory { get; set; } = string.Empty;

    public AudioFormat PreferredFormat { get; set; } = AudioFormat.Mp3;

    public int MaxParallelDownloads { get; set; } = 3;

    /// <summary>Kilobytes per second. 0 → unlimited.</summary>
    public int RateLimitKbps { get; set; }

    public bool AutoPasteClipboardLinks { get; set; } = true;

    public bool ShowNotifications { get; set; } = true;

    public bool CheckForUpdatesOnStartup { get; set; } = true;

    public AppThemeMode Theme { get; set; } = AppThemeMode.Dark;

    public AppSettingsModel Clone() => (AppSettingsModel)MemberwiseClone();
}
