namespace WaveDL.Models;

/// <summary>A YouTube / YouTube Music playlist or album with its (flat) track list.</summary>
public sealed class PlaylistInfo
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required IReadOnlyList<Track> Tracks { get; init; }
    public string? ThumbnailUrl { get; init; }

    public int Count => Tracks.Count;
}
