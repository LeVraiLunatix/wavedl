using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaveDL.Data;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services;

public sealed class HistoryService(
    IDbContextFactory<WaveDlDbContext> contextFactory,
    ILogger<HistoryService> logger) : IHistoryService
{
    public event EventHandler? Changed;

    public async Task<IReadOnlyList<HistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var rows = await db.History
                .AsNoTracking()
                .OrderByDescending(x => x.DownloadedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return rows.Select(Map).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Lecture de l'historique impossible.");
            return [];
        }
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var rows = await db.History
                .AsNoTracking()
                .OrderByDescending(x => x.DownloadedAt)
                .Take(Math.Max(1, count))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return rows.Select(Map).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Lecture de l'historique récent impossible.");
            return [];
        }
    }

    public async Task<HistoryEntry> AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = new HistoryRecord
        {
            Title = entry.Title,
            Artist = entry.Artist,
            Album = entry.Album,
            ThumbnailUrl = entry.ThumbnailUrl,
            FilePath = entry.FilePath,
            Format = entry.Format,
            Quality = entry.Quality,
            FileSizeBytes = entry.FileSizeBytes,
            DownloadedAt = entry.DownloadedAt == default ? DateTimeOffset.Now : entry.DownloadedAt,
            SourceUrl = entry.SourceUrl,
        };

        db.History.Add(record);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
        return Map(record);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var affected = await db.History
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            if (affected > 0)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Suppression de l'entrée d'historique {Id} impossible.", id);
        }
    }

    public async Task<int> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var affected = await db.History.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            if (affected > 0)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return affected;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Vidage de l'historique impossible.");
            return 0;
        }
    }

    private static HistoryEntry Map(HistoryRecord r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Artist = r.Artist,
        Album = r.Album,
        ThumbnailUrl = r.ThumbnailUrl,
        FilePath = r.FilePath,
        Format = r.Format,
        Quality = r.Quality,
        FileSizeBytes = r.FileSizeBytes,
        DownloadedAt = r.DownloadedAt,
        SourceUrl = r.SourceUrl,
    };
}
