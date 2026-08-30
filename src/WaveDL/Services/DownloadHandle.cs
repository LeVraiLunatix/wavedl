using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;
using WaveDL.Services.YtDlp;

namespace WaveDL.Services;

/// <summary>One download job. Drives yt-dlp, supports pause/resume (via <c>--continue</c>)
/// and retries, and publishes progress snapshots.</summary>
internal sealed class DownloadHandle : IDownloadHandle
{
    private const int MaxAttempts = 3;

    private readonly YtDlpEngine _engine;
    private readonly ISettingsService _settings;
    private readonly ILogger _logger;

    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<DownloadResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _files = [];
    private readonly object _filesLock = new();
    private readonly string _workDirectory;

    private volatile TaskCompletionSource _resumeGate = CompletedGate();
    private CancellationTokenSource? _processCts;
    private volatile bool _paused;
    private string? _lastError;

    public DownloadHandle(DownloadRequest request, YtDlpEngine engine, ISettingsService settings, ILogger logger)
    {
        Request = request;
        _engine = engine;
        _settings = settings;
        _logger = logger;
        _workDirectory = Path.Combine(settings.CacheDirectory, HashKey(request.Url, request.Format));
    }

    public Guid Id { get; } = Guid.NewGuid();

    public DownloadRequest Request { get; }

    public DownloadProgress Progress { get; private set; } = DownloadProgress.Initial;

    public Task<DownloadResult> Completion => _completion.Task;

    public event EventHandler<DownloadProgress>? ProgressChanged;

    public void Pause()
    {
        if (_paused || _completion.Task.IsCompleted)
        {
            return;
        }

        if (Progress.State is DownloadState.Completed or DownloadState.Failed or DownloadState.Canceled)
        {
            return;
        }

        _paused = true;
        _resumeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SafeCancel(_processCts);
        Emit(DownloadState.Paused, "En pause");
    }

    public void Resume()
    {
        if (!_paused)
        {
            return;
        }

        _paused = false;
        _resumeGate.TrySetResult();
        Emit(DownloadState.Preparing, "Reprise…");
    }

    public void Cancel()
    {
        if (_completion.Task.IsCompleted)
        {
            return;
        }

        _paused = false;
        SafeCancel(_cts);
        _resumeGate.TrySetResult();
        SafeCancel(_processCts);
    }

    internal async Task ExecuteAsync()
    {
        var attempt = 0;

        try
        {
            while (true)
            {
                _cts.Token.ThrowIfCancellationRequested();
                await _resumeGate.Task.WaitAsync(_cts.Token).ConfigureAwait(false);
                _cts.Token.ThrowIfCancellationRequested();

                _processCts?.Dispose();
                _processCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

                lock (_filesLock)
                {
                    _files.Clear();
                }

                Emit(DownloadState.Preparing, "Préparation…");

                int exitCode;
                try
                {
                    exitCode = await _engine.RunDownloadAsync(
                        Request,
                        DestinationDirectory,
                        _workDirectory,
                        attempt,
                        HandleStdout,
                        HandleStderr,
                        _processCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_paused && !_cts.IsCancellationRequested)
                {
                    Emit(DownloadState.Paused, "En pause");
                    continue;
                }

                if (_cts.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                if (exitCode == 0 && HasFiles())
                {
                    break;
                }

                if (_paused)
                {
                    continue;
                }

                attempt++;
                if (attempt >= MaxAttempts)
                {
                    throw new YtDlpException(_lastError ?? $"yt-dlp a échoué (code {exitCode}).");
                }

                Emit(DownloadState.Preparing, $"Nouvel essai {attempt}/{MaxAttempts}…");
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), _cts.Token).ConfigureAwait(false);
            }

            List<string> files;
            lock (_filesLock)
            {
                files = _files.Where(File.Exists).Distinct().ToList();
            }

            if (files.Count == 0)
            {
                throw new YtDlpException("Le fichier téléchargé est introuvable.");
            }

            var result = new DownloadResult
            {
                Files = files,
                TotalSizeBytes = files.Sum(SafeLength),
                FormatLabel = Request.Format.DisplayLabel(),
                QualityLabel = Request.Format.QualityLabel(),
            };

            Emit(DownloadState.Completed, "Terminé", percentOverride: 100);
            CleanupWorkDirectory();
            _completion.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            Emit(DownloadState.Canceled, "Annulé");
            CleanupWorkDirectory();
            _completion.TrySetCanceled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Téléchargement échoué : {Url}", Request.Url);
            Emit(DownloadState.Failed, ex.Message);
            _completion.TrySetException(ex);
        }
    }

    private string DestinationDirectory =>
        string.IsNullOrWhiteSpace(Request.DestinationDirectory)
            ? _settings.EffectiveDownloadDirectory
            : Request.DestinationDirectory!;

    private void HandleStdout(string line)
    {
        if (line.StartsWith(YtDlpEngine.ProgressPrefix, StringComparison.Ordinal))
        {
            ParseProgress(line[YtDlpEngine.ProgressPrefix.Length..]);
            return;
        }

        if (line.StartsWith(YtDlpEngine.FilePrefix, StringComparison.Ordinal))
        {
            var path = line[YtDlpEngine.FilePrefix.Length..].Trim();
            if (path.Length > 0)
            {
                lock (_filesLock)
                {
                    _files.Add(path);
                }
            }

            return;
        }

        if (line.Contains("[ExtractAudio]", StringComparison.Ordinal)
            || line.Contains("[Metadata]", StringComparison.Ordinal)
            || line.Contains("[EmbedThumbnail]", StringComparison.Ordinal))
        {
            Emit(DownloadState.Converting, "Conversion audio…");
        }
    }

    private void HandleStderr(string line)
    {
        if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
        {
            _lastError = line["ERROR:".Length..].Trim();
            _logger.LogWarning("yt-dlp: {Error}", _lastError);
        }
    }

    private void ParseProgress(string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length < 5)
        {
            return;
        }

        var downloaded = ParseNumber(parts[0]);
        var total = ParseNumber(parts[1]);
        var estimate = ParseNumber(parts[2]);
        var speed = ParseNumber(parts[3]);
        var eta = ParseNumber(parts[4]);
        var playlistIndex = parts.Length > 5 ? (int)ParseNumber(parts[5]) : 0;
        var playlistCount = parts.Length > 6 ? (int)ParseNumber(parts[6]) : 0;

        var denominator = total > 0 ? total : estimate;
        var percent = denominator > 0
            ? Math.Clamp(downloaded / denominator * 100.0, 0, 100)
            : Progress.Percent;

        var status = playlistCount > 1 ? $"Piste {playlistIndex}/{playlistCount}" : "Téléchargement…";

        Progress = new DownloadProgress(
            percent,
            (long)downloaded,
            (long)denominator,
            speed,
            eta > 0 ? TimeSpan.FromSeconds(eta) : TimeSpan.Zero,
            DownloadState.Downloading,
            status,
            playlistIndex,
            playlistCount);

        ProgressChanged?.Invoke(this, Progress);
    }

    private void Emit(DownloadState state, string? status, double? percentOverride = null)
    {
        Progress = Progress with
        {
            State = state,
            StatusText = status,
            Percent = percentOverride ?? Progress.Percent,
        };

        ProgressChanged?.Invoke(this, Progress);
    }

    private bool HasFiles()
    {
        lock (_filesLock)
        {
            return _files.Count > 0;
        }
    }

    private void CleanupWorkDirectory()
    {
        try
        {
            if (Directory.Exists(_workDirectory))
            {
                Directory.Delete(_workDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftover cache is pruned on the next run — not worth surfacing.
        }
    }

    private static double ParseNumber(string value)
    {
        if (string.IsNullOrEmpty(value) || value is "NA" or "None" or "N/A")
        {
            return 0;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static void SafeCancel(CancellationTokenSource? source)
    {
        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static TaskCompletionSource CompletedGate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }

    private static string HashKey(string url, AudioFormat format)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes($"{url}|{format}"));
        return Convert.ToHexString(bytes)[..16];
    }
}
