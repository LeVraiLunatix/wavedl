namespace WaveDL.Models;

/// <summary>Output audio container/codec the user can pick. The best source stream is always used.</summary>
public enum AudioFormat
{
    Mp3,
    Flac,
    Wav,
    Aac,
}

/// <summary>Lifecycle of a single download.</summary>
public enum DownloadState
{
    Queued,
    Preparing,
    Downloading,
    Converting,
    Paused,
    Completed,
    Failed,
    Canceled,
}

/// <summary>Classification of a pasted link.</summary>
public enum ExternalLinkKind
{
    /// <summary>Not a URL — treat as a search query.</summary>
    NotAUrl,
    Unknown,
    YouTube,
    YouTubeMusic,
    YouTubePlaylist,
    Spotify,
    Deezer,
    AppleMusic,
}

public enum AppThemeMode
{
    Dark,
    Light,
    System,
}
