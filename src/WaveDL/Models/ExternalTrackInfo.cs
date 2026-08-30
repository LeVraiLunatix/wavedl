namespace WaveDL.Models;

/// <summary>
/// Metadata identified from a Spotify / Deezer / Apple Music link. Used only to build a
/// YouTube Music search query — no protected stream is ever accessed on those platforms.
/// </summary>
public sealed class ExternalTrackInfo
{
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public string? Album { get; init; }
    public string? CoverUrl { get; init; }
    public TimeSpan? Duration { get; init; }
    public required ExternalLinkKind Source { get; init; }
    public string? OriginalUrl { get; init; }

    public string SearchQuery => string.Join(' ',
        new[] { Artist, Title }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    public string SourceLabel => Source switch
    {
        ExternalLinkKind.Spotify => "Spotify",
        ExternalLinkKind.Deezer => "Deezer",
        ExternalLinkKind.AppleMusic => "Apple Music",
        _ => "Lien",
    };
}
