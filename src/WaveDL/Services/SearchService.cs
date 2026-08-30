using Microsoft.Extensions.Logging;
using WaveDL.Helpers;
using WaveDL.Models;
using WaveDL.Services.Abstractions;
using WaveDL.Services.YtDlp;

namespace WaveDL.Services;

public sealed class SearchService(
    YtDlpEngine engine,
    ILogger<SearchService> logger) : ISearchService
{
    private static readonly string[] NoiseKeywords =
        ["live", "remix", "sped up", "slowed", "reverb", "cover", "mashup", "8d audio", "nightcore", "instrumental", "karaoke"];

    public Task<IReadOnlyList<Track>> SearchAsync(string query, int limit = 25, CancellationToken cancellationToken = default) =>
        engine.SearchAsync(query, limit, music: true, cancellationToken);

    public Task<Track?> ResolveTrackAsync(string url, CancellationToken cancellationToken = default) =>
        engine.ResolveTrackAsync(url, cancellationToken);

    public Task<PlaylistInfo?> ResolvePlaylistAsync(string url, CancellationToken cancellationToken = default) =>
        engine.ResolvePlaylistAsync(url, cancellationToken);

    public Task<IReadOnlyList<AudioStreamInfo>> GetAudioStreamsAsync(string url, CancellationToken cancellationToken = default) =>
        engine.GetAudioStreamsAsync(url, cancellationToken);

    public async Task<IReadOnlyList<MatchCandidate>> FindMatchesAsync(ExternalTrackInfo info, CancellationToken cancellationToken = default)
    {
        var query = info.SearchQuery;
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        IReadOnlyList<Track> results;
        try
        {
            results = await engine.SearchAsync(query, 12, music: true, cancellationToken).ConfigureAwait(false);
        }
        catch (YtDlpException ex)
        {
            logger.LogWarning(ex, "Recherche de correspondances impossible pour « {Query} ».", query);
            return [];
        }

        return results
            .Select(track => new MatchCandidate { Track = track, Confidence = Score(track, info) })
            .OrderByDescending(candidate => candidate.Confidence)
            .Take(8)
            .ToList();
    }

    private static double Score(Track track, ExternalTrackInfo info)
    {
        var titleSimilarity = Math.Max(
            TextMatch.TokenSetRatio(track.Title, info.Title),
            TextMatch.TokenSetRatio(track.Title, $"{info.Artist} {info.Title}"));

        var artistSimilarity = string.IsNullOrWhiteSpace(info.Artist)
            ? 0.5
            : Math.Max(
                TextMatch.TokenSetRatio(track.Artist, info.Artist),
                TextMatch.TokenSetRatio(track.Title, info.Artist));

        var durationScore = 0.5;
        if (info.Duration is { } expected && expected > TimeSpan.Zero && track.Duration > TimeSpan.Zero)
        {
            var deltaSeconds = Math.Abs((track.Duration - expected).TotalSeconds);
            durationScore = deltaSeconds <= 1 ? 1.0 : Math.Max(0, 1.0 - ((deltaSeconds - 1) / 20.0));
        }

        var bonus = 0.0;
        if (TextMatch.ContainsAny(track.Provider, "music") || TextMatch.ContainsAny(track.Artist, "topic"))
        {
            bonus += 0.08;
        }

        if (TextMatch.ContainsAny(track.Title, "official audio", "official music video", "official video"))
        {
            bonus += 0.04;
        }

        var penalty = 0.0;
        foreach (var keyword in NoiseKeywords)
        {
            if (TextMatch.ContainsAny(track.Title, keyword) && !TextMatch.ContainsAny(info.Title, keyword))
            {
                penalty += 0.12;
            }
        }

        var score = (0.50 * titleSimilarity) + (0.28 * artistSimilarity) + (0.22 * durationScore) + bonus - penalty;
        return Math.Clamp(score, 0, 1);
    }
}
