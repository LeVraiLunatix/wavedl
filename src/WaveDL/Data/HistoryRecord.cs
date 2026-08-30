namespace WaveDL.Data;

/// <summary>EF Core entity backing the download history table.</summary>
public sealed class HistoryRecord
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset DownloadedAt { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
}
