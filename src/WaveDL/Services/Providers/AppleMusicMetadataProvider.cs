using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services.Providers;

/// <summary>Resolves an Apple Music track link via the public iTunes lookup API (no key).</summary>
public sealed partial class AppleMusicMetadataProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<AppleMusicMetadataProvider> logger) : IExternalMetadataProvider
{
    [GeneratedRegex(@"[?&]i=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TrackQueryIdRegex();

    [GeneratedRegex(@"/song/[^/]+/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SongPathIdRegex();

    [GeneratedRegex(@"music\.apple\.com/([a-z]{2})/", RegexOptions.IgnoreCase)]
    private static partial Regex CountryRegex();

    [GeneratedRegex("<meta[^>]+property=\"og:([a-z:]+)\"[^>]+content=\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex OpenGraphRegex();

    public bool CanHandle(ExternalLinkKind kind) => kind == ExternalLinkKind.AppleMusic;

    public async Task<ExternalTrackInfo?> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var trackId = TrackQueryIdRegex().Match(url) is { Success: true } q
                ? q.Groups[1].Value
                : SongPathIdRegex().Match(url) is { Success: true } s
                    ? s.Groups[1].Value
                    : null;

            var country = CountryRegex().Match(url) is { Success: true } c ? c.Groups[1].Value : "us";

            using var client = httpClientFactory.CreateClient("default");

            if (trackId is not null)
            {
                var lookup = await LookupAsync(client, trackId, country, url, cancellationToken).ConfigureAwait(false);
                if (lookup is not null)
                {
                    return lookup;
                }
            }

            return await ResolveFromOpenGraphAsync(client, url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Résolution du lien Apple Music impossible.");
            return null;
        }
    }

    private static async Task<ExternalTrackInfo?> LookupAsync(
        HttpClient client,
        string trackId,
        string country,
        string originalUrl,
        CancellationToken cancellationToken)
    {
        await using var stream = await client
            .GetStreamAsync($"https://itunes.apple.com/lookup?id={trackId}&country={country}&entity=song", cancellationToken)
            .ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            return null;
        }

        var track = results.EnumerateArray()
            .FirstOrDefault(r => r.TryGetProperty("kind", out var kind) && kind.GetString() == "song");
        if (track.ValueKind != JsonValueKind.Object)
        {
            track = results[0];
        }

        var title = track.TryGetProperty("trackName", out var titleEl) ? titleEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var artist = track.TryGetProperty("artistName", out var artistEl) ? artistEl.GetString() : null;
        var album = track.TryGetProperty("collectionName", out var albumEl) ? albumEl.GetString() : null;
        var artwork = track.TryGetProperty("artworkUrl100", out var artEl) ? artEl.GetString() : null;
        var millis = track.TryGetProperty("trackTimeMillis", out var msEl) && msEl.TryGetInt64(out var ms) ? ms : 0;

        return new ExternalTrackInfo
        {
            Title = title!,
            Artist = artist ?? string.Empty,
            Album = album,
            CoverUrl = artwork?.Replace("100x100bb", "600x600bb", StringComparison.Ordinal),
            Duration = millis > 0 ? TimeSpan.FromMilliseconds(millis) : null,
            Source = ExternalLinkKind.AppleMusic,
            OriginalUrl = originalUrl,
        };
    }

    private async Task<ExternalTrackInfo?> ResolveFromOpenGraphAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var html = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        var tags = OpenGraphRegex().Matches(html)
            .ToDictionary(m => m.Groups[1].Value.ToLowerInvariant(), m => System.Net.WebUtility.HtmlDecode(m.Groups[2].Value));

        if (!tags.TryGetValue("title", out var ogTitle) || string.IsNullOrWhiteSpace(ogTitle))
        {
            return null;
        }

        // Apple's og:title is usually "Song by Artist on Apple Music".
        var title = ogTitle;
        var artist = string.Empty;
        var byIndex = ogTitle.IndexOf(" by ", StringComparison.OrdinalIgnoreCase);
        var onIndex = ogTitle.LastIndexOf(" on ", StringComparison.OrdinalIgnoreCase);
        if (byIndex > 0)
        {
            title = ogTitle[..byIndex].Trim();
            var tail = onIndex > byIndex ? ogTitle[(byIndex + 4)..onIndex] : ogTitle[(byIndex + 4)..];
            artist = tail.Trim();
        }

        return new ExternalTrackInfo
        {
            Title = title,
            Artist = artist,
            Album = tags.GetValueOrDefault("music:album"),
            CoverUrl = tags.GetValueOrDefault("image"),
            Duration = null,
            Source = ExternalLinkKind.AppleMusic,
            OriginalUrl = url,
        };
    }
}
