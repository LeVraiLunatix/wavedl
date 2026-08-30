using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services.Providers;

/// <summary>Resolves a Deezer track link via the public, key-less Deezer API.</summary>
public sealed partial class DeezerMetadataProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<DeezerMetadataProvider> logger) : IExternalMetadataProvider
{
    [GeneratedRegex(@"/track/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TrackIdRegex();

    public bool CanHandle(ExternalLinkKind kind) => kind == ExternalLinkKind.Deezer;

    public async Task<ExternalTrackInfo?> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("default");

            var resolvedUrl = url;
            var match = TrackIdRegex().Match(resolvedUrl);
            if (!match.Success)
            {
                // Short links (deezer.page.link / dzr.page.link) — follow the redirect.
                using var head = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                resolvedUrl = head.RequestMessage?.RequestUri?.ToString() ?? url;
                match = TrackIdRegex().Match(resolvedUrl);
            }

            if (!match.Success)
            {
                logger.LogWarning("Identifiant de piste Deezer introuvable dans {Url}.", url);
                return null;
            }

            var id = match.Groups[1].Value;
            await using var stream = await client.GetStreamAsync($"https://api.deezer.com/track/{id}", cancellationToken)
                .ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                return null;
            }

            var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
            var artist = root.TryGetProperty("artist", out var artistEl) && artistEl.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString()
                : null;
            var album = root.TryGetProperty("album", out var albumEl) && albumEl.TryGetProperty("title", out var albumTitleEl)
                ? albumTitleEl.GetString()
                : null;
            var cover = albumEl.ValueKind == JsonValueKind.Object && albumEl.TryGetProperty("cover_xl", out var coverEl)
                ? coverEl.GetString()
                : null;
            var durationSeconds = root.TryGetProperty("duration", out var durEl) && durEl.TryGetInt32(out var seconds)
                ? seconds
                : 0;

            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            return new ExternalTrackInfo
            {
                Title = title!,
                Artist = artist ?? string.Empty,
                Album = album,
                CoverUrl = cover,
                Duration = durationSeconds > 0 ? TimeSpan.FromSeconds(durationSeconds) : null,
                Source = ExternalLinkKind.Deezer,
                OriginalUrl = url,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Résolution du lien Deezer impossible.");
            return null;
        }
    }
}
