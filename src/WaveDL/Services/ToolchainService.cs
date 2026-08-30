using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WaveDL.Services.Abstractions;
using WaveDL.Services.YtDlp;

namespace WaveDL.Services;

/// <summary>
/// Locates <c>yt-dlp.exe</c> and <c>ffmpeg.exe</c> (app folder → local bin → PATH) and can
/// download them on demand from their official GitHub releases.
/// </summary>
public sealed class ToolchainService(
    ISettingsService settings,
    IHttpClientFactory httpClientFactory,
    ILogger<ToolchainService> logger) : IToolchainService
{
    // Canal « nightly » : yt-dlp suit les changements de YouTube au jour le jour, la release stable
    // accuse souvent plusieurs semaines de retard (source classique d'erreurs 403).
    private const string YtDlpDownloadUrl =
        "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe";

    private readonly SemaphoreSlim _mutex = new(1, 1);

    public string? YtDlpPath { get; private set; }

    public string? FfmpegPath { get; private set; }

    public string? FfmpegDirectory => FfmpegPath is null ? null : Path.GetDirectoryName(FfmpegPath);

    public bool IsReady => YtDlpPath is not null && FfmpegPath is not null;

    public async Task<ToolchainStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        YtDlpPath = Locate("yt-dlp.exe");
        FfmpegPath = Locate("ffmpeg.exe");

        string? version = null;
        if (YtDlpPath is not null)
        {
            version = await GetYtDlpVersionAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ToolchainStatus(YtDlpPath is not null, YtDlpPath, version, FfmpegPath is not null, FfmpegPath);
    }

    public async Task EnsureReadyAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(settings.BinDirectory);

            YtDlpPath ??= Locate("yt-dlp.exe");
            if (YtDlpPath is null)
            {
                progress?.Report("Téléchargement de yt-dlp…");
                var target = Path.Combine(settings.BinDirectory, "yt-dlp.exe");
                await DownloadFileAsync(YtDlpDownloadUrl, target, progress, cancellationToken).ConfigureAwait(false);
                YtDlpPath = target;
            }

            FfmpegPath ??= Locate("ffmpeg.exe");
            if (FfmpegPath is null)
            {
                progress?.Report("Téléchargement de FFmpeg (~40 Mo)…");
                await DownloadFfmpegAsync(progress, cancellationToken).ConfigureAwait(false);
                FfmpegPath = Locate("ffmpeg.exe");
            }

            if (!IsReady)
            {
                throw new YtDlpException("Installation des composants incomplète. Réessayez ou installez yt-dlp / FFmpeg manuellement.");
            }

            progress?.Report("Composants prêts.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpdateYtDlpAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progress?.Report("Mise à jour de yt-dlp…");
            var target = Path.Combine(settings.BinDirectory, "yt-dlp.exe");
            await DownloadFileAsync(YtDlpDownloadUrl, target, progress, cancellationToken).ConfigureAwait(false);
            YtDlpPath = target;
            progress?.Report("yt-dlp à jour.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<string?> GetYtDlpVersionAsync(CancellationToken cancellationToken = default)
    {
        if (YtDlpPath is null)
        {
            return null;
        }

        try
        {
            var result = await ProcessRunner.RunAsync(YtDlpPath, ["--version"], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
        }
        catch (YtDlpException)
        {
            return null;
        }
    }

    private string? Locate(string fileName)
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(settings.BinDirectory, fileName),
        };

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                candidates.Add(Path.Combine(directory, fileName));
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry — skip it.
            }
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task DownloadFfmpegAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var assetName = RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? "ffmpeg-master-latest-winarm64-gpl.zip"
            : "ffmpeg-master-latest-win64-gpl.zip";
        var zipUrl = $"https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/{assetName}";

        var tempZip = Path.Combine(settings.CacheDirectory, $"ffmpeg-{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadFileAsync(zipUrl, tempZip, progress, cancellationToken).ConfigureAwait(false);

            progress?.Report("Extraction de FFmpeg…");
            using var archive = ZipFile.OpenRead(tempZip);
            foreach (var wanted in new[] { "ffmpeg.exe", "ffprobe.exe" })
            {
                var entry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)
                    && e.FullName.Contains("/bin/", StringComparison.OrdinalIgnoreCase));

                if (entry is null)
                {
                    continue;
                }

                var destination = Path.Combine(settings.BinDirectory, wanted);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }
        finally
        {
            TryDelete(tempZip);
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("default");
        client.Timeout = TimeSpan.FromMinutes(10);

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var tempPath = destinationPath + ".part";

        await using (var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long received = 0;
            var lastReport = DateTime.MinValue;
            int read;

            while ((read = await httpStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;

                if (totalBytes > 0 && DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(400))
                {
                    lastReport = DateTime.UtcNow;
                    var percent = received * 100.0 / totalBytes;
                    progress?.Report($"Téléchargement… {percent:0} %");
                }
            }
        }

        File.Move(tempPath, destinationPath, overwrite: true);
        logger.LogInformation("Composant téléchargé : {Path}", destinationPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
