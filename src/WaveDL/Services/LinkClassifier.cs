using WaveDL.Models;

namespace WaveDL.Services;

/// <summary>Pure URL classification shared by the metadata and clipboard services.</summary>
public static class LinkClassifier
{
    public static bool IsUrl(string? input) =>
        !string.IsNullOrWhiteSpace(input)
        && Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static ExternalLinkKind Classify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || !IsUrl(input))
        {
            return ExternalLinkKind.NotAUrl;
        }

        var uri = new Uri(input.Trim());
        var host = uri.Host.ToLowerInvariant();
        var query = uri.Query.ToLowerInvariant();
        var path = uri.AbsolutePath.ToLowerInvariant();

        if (host is "open.spotify.com" or "spotify.link" || host.EndsWith(".spotify.com", StringComparison.Ordinal))
        {
            return ExternalLinkKind.Spotify;
        }

        if (host.Contains("deezer.com", StringComparison.Ordinal)
            || host.Contains("deezer.page.link", StringComparison.Ordinal)
            || host.Contains("dzr.page.link", StringComparison.Ordinal))
        {
            return ExternalLinkKind.Deezer;
        }

        if (host is "music.apple.com" || host.EndsWith(".music.apple.com", StringComparison.Ordinal))
        {
            return ExternalLinkKind.AppleMusic;
        }

        if (host is "music.youtube.com")
        {
            return path.Contains("playlist", StringComparison.Ordinal) && !query.Contains("v=", StringComparison.Ordinal)
                ? ExternalLinkKind.YouTubePlaylist
                : ExternalLinkKind.YouTubeMusic;
        }

        if (host is "youtu.be" || host.Contains("youtube.com", StringComparison.Ordinal))
        {
            var hasList = query.Contains("list=", StringComparison.Ordinal);
            var hasVideo = query.Contains("v=", StringComparison.Ordinal) || host is "youtu.be";
            return hasList && (!hasVideo || path.Contains("playlist", StringComparison.Ordinal))
                ? ExternalLinkKind.YouTubePlaylist
                : ExternalLinkKind.YouTube;
        }

        return ExternalLinkKind.Unknown;
    }

    public static bool IsSupported(string? input) => Classify(input) is not ExternalLinkKind.NotAUrl;

    public static bool IsYouTubeFamily(ExternalLinkKind kind) =>
        kind is ExternalLinkKind.YouTube or ExternalLinkKind.YouTubeMusic or ExternalLinkKind.YouTubePlaylist;
}
