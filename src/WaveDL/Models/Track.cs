namespace WaveDL.Models;

/// <summary>A playable item on YouTube / YouTube Music (search result or resolved link).</summary>
public sealed class Track
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public string? Album { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ThumbnailUrl { get; init; }
    public required string SourceUrl { get; init; }
    public string Provider { get; init; } = "YouTube";

    public string DurationText => Duration > TimeSpan.Zero
        ? (Duration.TotalHours >= 1 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"m\:ss"))
        : "--:--";

    public string BestThumbnailUrl => string.IsNullOrWhiteSpace(ThumbnailUrl)
        ? $"https://i.ytimg.com/vi/{Id}/hqdefault.jpg"
        : ThumbnailUrl!;
}
