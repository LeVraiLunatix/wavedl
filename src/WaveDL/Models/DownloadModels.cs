namespace WaveDL.Models;

/// <summary>Everything needed to start one download job.</summary>
public sealed class DownloadRequest
{
    public required string Url { get; init; }
    public required AudioFormat Format { get; init; }
    public string? DisplayTitle { get; init; }
    public string? DisplayArtist { get; init; }
    public string? ThumbnailUrl { get; init; }

    /// <summary>Null → use the configured default download directory.</summary>
    public string? DestinationDirectory { get; init; }

    public bool IsPlaylist { get; init; }

    /// <summary>Null → use the configured global rate limit. 0 → unlimited.</summary>
    public int? RateLimitKbps { get; init; }
}

/// <summary>Immutable progress snapshot pushed by the download engine.</summary>
public readonly record struct DownloadProgress(
    double Percent,
    long DownloadedBytes,
    long TotalBytes,
    double SpeedBytesPerSecond,
    TimeSpan Eta,
    DownloadState State,
    string? StatusText,
    int PlaylistIndex = 0,
    int PlaylistCount = 0)
{
    public static DownloadProgress Initial { get; } =
        new(0, 0, 0, 0, TimeSpan.Zero, DownloadState.Queued, "En file d'attente");
}

/// <summary>Result of a completed download.</summary>
public sealed class DownloadResult
{
    public required IReadOnlyList<string> Files { get; init; }
    public required long TotalSizeBytes { get; init; }
    public required string FormatLabel { get; init; }
    public required string QualityLabel { get; init; }

    public string PrimaryFile => Files.Count > 0 ? Files[0] : string.Empty;
}
