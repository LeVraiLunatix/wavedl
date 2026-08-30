using System.Text.Json;
using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services.YtDlp;

/// <summary>
/// Owns every invocation of the yt-dlp executable: search, link resolution, format probing
/// and the actual audio download. UI-facing services depend on this type rather than on the
/// process details.
/// </summary>
public sealed class YtDlpEngine(
    IToolchainService toolchain,
    ISettingsService settings,
    ILogger<YtDlpEngine> logger)
{
    public const string ProgressPrefix = "WAVEDL_PROG:";
    public const string FilePrefix = "WAVEDL_FILE:";

    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(70);

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var exe = toolchain.YtDlpPath;
        if (exe is null)
        {
            return null;
        }

        var result = await ProcessRunner.RunAsync(exe, ["--version"], cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    public async Task<IReadOnlyList<Track>> SearchAsync(
        string query,
        int limit,
        bool music,
        CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 50);

        if (music)
        {
            var musicUrl = $"https://music.youtube.com/search?q={Uri.EscapeDataString(query)}#Songs";
            try
            {
                var musicResults = await RunSearchAsync(musicUrl, music: true, limit, cancellationToken).ConfigureAwait(false);
                if (musicResults.Count > 0)
                {
                    return musicResults.Take(limit).ToList();
                }
            }
            catch (YtDlpException ex)
            {
                logger.LogWarning(ex, "Recherche YouTube Music indisponible, repli sur YouTube.");
            }
        }

        var fallback = await RunSearchAsync($"ytsearch{limit}:{query}", music: false, limit, cancellationToken).ConfigureAwait(false);
        return fallback.Take(limit).ToList();
    }

    public async Task<Track?> ResolveTrackAsync(string url, CancellationToken cancellationToken = default)
    {
        var music = LinkClassifier.Classify(url) == ExternalLinkKind.YouTubeMusic;
        var root = await DumpJsonAsync([url, "--no-playlist"], cancellationToken).ConfigureAwait(false);
        return root is null ? null : YtDlpParser.ParseTrack(root.Value, music);
    }

    public async Task<PlaylistInfo?> ResolvePlaylistAsync(string url, CancellationToken cancellationToken = default)
    {
        var music = LinkClassifier.Classify(url) is ExternalLinkKind.YouTubeMusic or ExternalLinkKind.YouTubePlaylist
            && url.Contains("music.youtube.com", StringComparison.OrdinalIgnoreCase);

        var root = await DumpJsonAsync([url, "--yes-playlist", "--flat-playlist"], cancellationToken).ConfigureAwait(false);
        if (root is null || !root.Value.TryGetProperty("entries", out _))
        {
            return null;
        }

        return YtDlpParser.ParsePlaylist(root.Value, url, music);
    }

    public async Task<IReadOnlyList<AudioStreamInfo>> GetAudioStreamsAsync(string url, CancellationToken cancellationToken = default)
    {
        var root = await DumpJsonAsync([url, "--no-playlist"], cancellationToken).ConfigureAwait(false);
        return root is null ? [] : YtDlpParser.ParseAudioStreams(root.Value);
    }

    public async Task<int> RunDownloadAsync(
        DownloadRequest request,
        string destinationDirectory,
        string workingDirectory,
        int attempt,
        Action<string> onStdout,
        Action<string> onStderr,
        CancellationToken cancellationToken)
    {
        var exe = RequireYtDlp();
        Directory.CreateDirectory(destinationDirectory);
        Directory.CreateDirectory(workingDirectory);

        var effectiveRate = request.RateLimitKbps ?? settings.Current.RateLimitKbps;
        var arguments = BuildDownloadArguments(request, destinationDirectory, workingDirectory, effectiveRate, attempt);

        var result = await ProcessRunner.RunAsync(
            exe,
            arguments,
            workingDirectory,
            onStdout,
            onStderr,
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode;
    }

    private List<string> BuildDownloadArguments(
        DownloadRequest request,
        string destinationDirectory,
        string workingDirectory,
        int rateKbps,
        int attempt)
    {
        var format = request.Format;
        var arguments = new List<string>(BaseArguments())
        {
            "--newline",
            "--retries", "5",
            "--fragment-retries", "5",
            "--file-access-retries", "3",
            "--continue",
            "--no-overwrites",
            "--extract-audio",
            "--audio-format", format.ToYtDlpAudioFormat(),
            "--embed-metadata",
            "--add-metadata",
        };

        // YouTube bloque régulièrement certains « player clients » (erreurs 403 sur les données
        // média). On élargit les stratégies d'extraction à chaque nouvel essai.
        switch (attempt)
        {
            case 1:
                arguments.Add("--extractor-args");
                arguments.Add("youtube:player_client=tv,web_safari,web");
                break;
            case >= 2:
                arguments.Add("--extractor-args");
                arguments.Add("youtube:player_client=tv,web_safari,web,android,ios");
                arguments.Add("--force-ipv4");
                break;
        }

        if (format.ToYtDlpAudioQuality() is { } quality)
        {
            arguments.Add("--audio-quality");
            arguments.Add(quality);
        }

        if (format.SupportsEmbeddedArtwork())
        {
            arguments.Add("--embed-thumbnail");
            arguments.Add("--convert-thumbnails");
            arguments.Add("jpg");
        }

        arguments.Add(request.IsPlaylist ? "--yes-playlist" : "--no-playlist");

        if (rateKbps > 0)
        {
            arguments.Add("--limit-rate");
            arguments.Add($"{rateKbps}K");
        }

        if (toolchain.FfmpegDirectory is { } ffmpegDir)
        {
            arguments.Add("--ffmpeg-location");
            arguments.Add(ffmpegDir);
        }

        arguments.Add("--paths");
        arguments.Add($"home:{destinationDirectory}");
        arguments.Add("--paths");
        arguments.Add($"temp:{workingDirectory}");
        arguments.Add("--output");
        arguments.Add("%(title).180B [%(id)s].%(ext)s");

        arguments.Add("--progress-template");
        arguments.Add(ProgressPrefix +
            "%(progress.downloaded_bytes)s:%(progress.total_bytes)s:%(progress.total_bytes_estimate)s:" +
            "%(progress.speed)s:%(progress.eta)s:%(info.playlist_index)s:%(info.n_entries)s");

        arguments.Add("--print");
        arguments.Add($"after_move:{FilePrefix}%(filepath)s");

        arguments.Add(request.Url);
        return arguments;
    }

    private async Task<IReadOnlyList<Track>> RunSearchAsync(string target, bool music, int limit, CancellationToken cancellationToken)
    {
        var root = await DumpJsonAsync([target, "--flat-playlist", "--playlist-end", limit.ToString()], cancellationToken)
            .ConfigureAwait(false);

        return root is null ? [] : YtDlpParser.ParseSearchEntries(root.Value, music);
    }

    private async Task<JsonElement?> DumpJsonAsync(IEnumerable<string> targetArguments, CancellationToken cancellationToken)
    {
        var exe = RequireYtDlp();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(MetadataTimeout);

        var arguments = new List<string>(BaseArguments()) { "-J" };
        arguments.AddRange(targetArguments);

        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync(exe, arguments, cancellationToken: linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new YtDlpException("yt-dlp n'a pas répondu à temps.");
        }

        if (result.ExitCode != 0)
        {
            var reason = FirstError(result.StandardError);
            throw new YtDlpException(reason ?? $"yt-dlp a renvoyé le code {result.ExitCode}.");
        }

        var payload = result.StandardOutput.Trim();
        if (payload.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Réponse JSON yt-dlp illisible.");
            throw new YtDlpException("Réponse de yt-dlp illisible.", ex);
        }
    }

    private static IReadOnlyList<string> BaseArguments() =>
        ["--ignore-config", "--no-warnings", "--no-colors", "--no-progress", "--socket-timeout", "20"];

    private static string? FirstError(string standardError)
    {
        foreach (var line in standardError.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                return line["ERROR:".Length..].Trim();
            }
        }

        return null;
    }

    private string RequireYtDlp() =>
        toolchain.YtDlpPath
        ?? throw new YtDlpException("yt-dlp n'est pas installé. Ouvrez les Paramètres pour l'installer automatiquement.");
}
