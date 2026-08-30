using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services.Providers;

/// <summary>
/// Identifies a Spotify track from its public web page (JSON-LD, then Open Graph tags).
/// No Spotify audio stream is ever requested — only descriptive metadata.
/// </summary>
public sealed partial class SpotifyMetadataProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<SpotifyMetadataProvider> logger) : IExternalMetadataProvider
{
    [GeneratedRegex("<script[^>]+type=\"application/ld\\+json\"[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LdJsonRegex();

    [GeneratedRegex("<meta[^>]+property=\"og:([a-z:]+)\"[^>]+content=\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex OpenGraphRegex();

    public bool CanHandle(ExternalLinkKind kind) => kind == ExternalLinkKind.Spotify;

    public async Task<ExternalTrackInfo?> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("default");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en;q=0.9,fr;q=0.8");

            var html = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            return ParseJsonLd(html, url) ?? ParseOpenGraph(html, url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Résolution du lien Spotify impossible.");
            return null;
        }
    }

    private ExternalTrackInfo? ParseJsonLd(string html, string url)
    {
        foreach (Match match in LdJsonRegex().Matches(html))
        {
            var payload = match.Groups[1].Value.Trim();
            if (payload.Length == 0)
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    root = root.EnumerateArray().FirstOrDefault();
                }

                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = root.TryGetProperty("@type", out var typeEl) ? typeEl.GetString() : null;
                if (!string.Equals(type, "MusicRecording", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var title = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                return new ExternalTrackInfo
                {
                    Title = title!,
                    Artist = ReadArtists(root),
                    Album = root.TryGetProperty("inAlbum", out var albumEl) && albumEl.TryGetProperty("name", out var albumNameEl)
                        ? albumNameEl.GetString()
                        : null,
                    CoverUrl = ReadImage(root),
                    Duration = ReadDuration(root),
                    Source = ExternalLinkKind.Spotify,
                    OriginalUrl = url,
                };
            }
            catch (JsonException)
            {
                // Try the next script block / fall back to Open Graph.
            }
        }

        return null;
    }

    private static ExternalTrackInfo? ParseOpenGraph(string html, string url)
    {
        var tags = OpenGraphRegex().Matches(html)
            .ToDictionary(m => m.Groups[1].Value.ToLowerInvariant(), m => System.Net.WebUtility.HtmlDecode(m.Groups[2].Value));

        if (!tags.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var artist = string.Empty;
        if (tags.TryGetValue("description", out var description) && !string.IsNullOrWhiteSpace(description))
        {
            // "Artist · Song · 2023" or "Song · Artist · Album · 2023".
            var parts = description.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            artist = parts.FirstOrDefault(p => !p.Equals(title, StringComparison.OrdinalIgnoreCase) && !p.All(char.IsDigit))
                     ?? string.Empty;
        }

        return new ExternalTrackInfo
        {
            Title = title,
            Artist = artist,
            Album = tags.GetValueOrDefault("music:album"),
            CoverUrl = tags.GetValueOrDefault("image"),
            Duration = null,
            Source = ExternalLinkKind.Spotify,
            OriginalUrl = url,
        };
    }

    private static string ReadArtists(JsonElement root)
    {
        if (!root.TryGetProperty("byArtist", out var byArtist))
        {
            return string.Empty;
        }

        if (byArtist.ValueKind == JsonValueKind.Array)
        {
            var names = byArtist.EnumerateArray()
                .Select(a => a.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(n => !string.IsNullOrWhiteSpace(n));
            return string.Join(", ", names);
        }

        return byArtist.TryGetProperty("name", out var single) ? single.GetString() ?? string.Empty : string.Empty;
    }

    private static string? ReadImage(JsonElement root)
    {
        if (!root.TryGetProperty("image", out var image))
        {
            return null;
        }

        return image.ValueKind switch
        {
            JsonValueKind.String => image.GetString(),
            JsonValueKind.Array => image.EnumerateArray().FirstOrDefault().GetString(),
            _ => null,
        };
    }

    private static TimeSpan? ReadDuration(JsonElement root)
    {
        if (!root.TryGetProperty("duration", out var durationEl) || durationEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return XmlConvert.ToTimeSpan(durationEl.GetString()!);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
