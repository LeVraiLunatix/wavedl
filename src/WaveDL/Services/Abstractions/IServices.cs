using Microsoft.UI.Xaml.Controls;
using WaveDL.Models;

namespace WaveDL.Services.Abstractions;

// ---------------------------------------------------------------------------
// Settings
// ---------------------------------------------------------------------------
public interface ISettingsService
{
    AppSettingsModel Current { get; }

    string DataDirectory { get; }
    string BinDirectory { get; }
    string CacheDirectory { get; }
    string LogDirectory { get; }
    string DatabasePath { get; }
    string DefaultDownloadDirectory { get; }

    /// <summary>Configured directory if set and valid, otherwise the default. Created if missing.</summary>
    string EffectiveDownloadDirectory { get; }

    void Load();
    Task SaveAsync(CancellationToken cancellationToken = default);

    event EventHandler<AppSettingsModel>? Changed;
}

// ---------------------------------------------------------------------------
// External toolchain (yt-dlp + ffmpeg)
// ---------------------------------------------------------------------------
public sealed record ToolchainStatus(
    bool YtDlpInstalled,
    string? YtDlpPath,
    string? YtDlpVersion,
    bool FfmpegInstalled,
    string? FfmpegPath)
{
    public bool IsReady => YtDlpInstalled && FfmpegInstalled;
}

public interface IToolchainService
{
    string? YtDlpPath { get; }
    string? FfmpegPath { get; }
    string? FfmpegDirectory { get; }
    bool IsReady { get; }

    Task<ToolchainStatus> RefreshAsync(CancellationToken cancellationToken = default);
    Task EnsureReadyAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task UpdateYtDlpAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task<string?> GetYtDlpVersionAsync(CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Notifications
// ---------------------------------------------------------------------------
public interface INotificationService
{
    void TryRegister();
    void NotifyDownloadCompleted(string title, string artist, string filePath);
    void NotifyDownloadFailed(string title, string reason);
    void NotifyInformation(string title, string message);
}

// ---------------------------------------------------------------------------
// Clipboard
// ---------------------------------------------------------------------------
public interface IClipboardService
{
    void Start();
    void Stop();
    Task<string?> GetTextAsync();

    event EventHandler<string>? SupportedLinkDetected;
}

// ---------------------------------------------------------------------------
// History
// ---------------------------------------------------------------------------
public interface IHistoryService
{
    Task<IReadOnlyList<HistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<HistoryEntry> AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> ClearAsync(CancellationToken cancellationToken = default);

    event EventHandler? Changed;
}

// ---------------------------------------------------------------------------
// Metadata (Spotify / Deezer / Apple Music identification)
// ---------------------------------------------------------------------------
public interface IExternalMetadataProvider
{
    bool CanHandle(ExternalLinkKind kind);
    Task<ExternalTrackInfo?> ResolveAsync(string url, CancellationToken cancellationToken = default);
}

public interface IMetadataService
{
    ExternalLinkKind Classify(string input);
    Task<ExternalTrackInfo?> ResolveExternalAsync(string url, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Search
// ---------------------------------------------------------------------------
public interface ISearchService
{
    Task<IReadOnlyList<Track>> SearchAsync(string query, int limit = 25, CancellationToken cancellationToken = default);
    Task<Track?> ResolveTrackAsync(string url, CancellationToken cancellationToken = default);
    Task<PlaylistInfo?> ResolvePlaylistAsync(string url, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioStreamInfo>> GetAudioStreamsAsync(string url, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchCandidate>> FindMatchesAsync(ExternalTrackInfo info, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Download engine
// ---------------------------------------------------------------------------
public interface IDownloadHandle
{
    Guid Id { get; }
    DownloadRequest Request { get; }
    DownloadProgress Progress { get; }
    Task<DownloadResult> Completion { get; }

    void Pause();
    void Resume();
    void Cancel();

    event EventHandler<DownloadProgress>? ProgressChanged;
}

public interface IDownloadService
{
    IReadOnlyList<IDownloadHandle> Handles { get; }

    IDownloadHandle Enqueue(DownloadRequest request);

    event EventHandler<IDownloadHandle>? DownloadAdded;
}

// ---------------------------------------------------------------------------
// Updates
// ---------------------------------------------------------------------------
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? ReleaseNotes);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Navigation
// ---------------------------------------------------------------------------
public interface INavigationService
{
    bool CanGoBack { get; }

    void Register(string key, Type pageType);
    void Initialize(Frame frame);
    bool Navigate(string key, object? parameter = null);
    void GoBack();

    event EventHandler<string>? Navigated;
}
