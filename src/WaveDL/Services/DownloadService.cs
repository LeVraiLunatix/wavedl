using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;
using WaveDL.Services.YtDlp;

namespace WaveDL.Services;

/// <summary>
/// Queues download jobs and runs up to <c>MaxParallelDownloads</c> at a time. On completion
/// it records history and raises a Windows notification.
/// </summary>
public sealed class DownloadService(
    YtDlpEngine engine,
    ISettingsService settings,
    IHistoryService history,
    INotificationService notifications,
    ILogger<DownloadService> logger,
    ILoggerFactory loggerFactory) : IDownloadService
{
    private readonly List<DownloadHandle> _handles = [];
    private readonly Queue<DownloadHandle> _queue = new();
    private readonly object _gate = new();
    private int _running;

    public event EventHandler<IDownloadHandle>? DownloadAdded;

    public IReadOnlyList<IDownloadHandle> Handles
    {
        get
        {
            lock (_gate)
            {
                return _handles.ToArray();
            }
        }
    }

    public IDownloadHandle Enqueue(DownloadRequest request)
    {
        var handle = new DownloadHandle(request, engine, settings, loggerFactory.CreateLogger<DownloadHandle>());

        lock (_gate)
        {
            _handles.Add(handle);
            _queue.Enqueue(handle);
        }

        DownloadAdded?.Invoke(this, handle);
        PumpQueue();
        return handle;
    }

    private void PumpQueue()
    {
        List<DownloadHandle> toStart = [];

        lock (_gate)
        {
            var max = Math.Clamp(settings.Current.MaxParallelDownloads, 1, 8);
            while (_running < max && _queue.Count > 0)
            {
                var next = _queue.Dequeue();
                _running++;
                toStart.Add(next);
            }
        }

        foreach (var handle in toStart)
        {
            _ = RunHandleAsync(handle);
        }
    }

    private async Task RunHandleAsync(DownloadHandle handle)
    {
        try
        {
            await handle.ExecuteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Boucle de téléchargement interrompue de façon inattendue.");
        }
        finally
        {
            lock (_gate)
            {
                _running = Math.Max(0, _running - 1);
            }

            PumpQueue();
        }

        await FinalizeAsync(handle).ConfigureAwait(false);
    }

    private async Task FinalizeAsync(DownloadHandle handle)
    {
        var task = handle.Completion;

        if (task.IsCompletedSuccessfully)
        {
            await RecordHistoryAsync(handle, task.Result).ConfigureAwait(false);

            var main = task.Result.PrimaryFile;
            notifications.NotifyDownloadCompleted(
                handle.Request.DisplayTitle ?? Path.GetFileNameWithoutExtension(main),
                handle.Request.DisplayArtist ?? string.Empty,
                main);
        }
        else if (task.IsFaulted)
        {
            var message = task.Exception?.GetBaseException().Message ?? "Erreur inconnue.";
            notifications.NotifyDownloadFailed(handle.Request.DisplayTitle ?? "Téléchargement", message);
        }
    }

    private async Task RecordHistoryAsync(DownloadHandle handle, DownloadResult result)
    {
        try
        {
            var isPlaylist = result.Files.Count > 1;
            foreach (var file in result.Files)
            {
                var entry = new HistoryEntry
                {
                    Title = isPlaylist
                        ? Path.GetFileNameWithoutExtension(file)
                        : handle.Request.DisplayTitle ?? Path.GetFileNameWithoutExtension(file),
                    Artist = handle.Request.DisplayArtist ?? string.Empty,
                    ThumbnailUrl = handle.Request.ThumbnailUrl,
                    FilePath = file,
                    Format = result.FormatLabel,
                    Quality = result.QualityLabel,
                    FileSizeBytes = SafeLength(file),
                    DownloadedAt = DateTimeOffset.Now,
                    SourceUrl = handle.Request.Url,
                };

                await history.AddAsync(entry).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Écriture de l'historique impossible.");
        }
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
}
