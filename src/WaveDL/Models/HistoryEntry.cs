namespace WaveDL.Models;

/// <summary>A completed download, as shown on the History page.</summary>
public sealed class HistoryEntry
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTimeOffset DownloadedAt { get; init; }
    public string SourceUrl { get; init; } = string.Empty;

    public string DirectoryPath =>
        string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetDirectoryName(FilePath) ?? string.Empty;

    public bool FileExists => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    public string DateText => DownloadedAt.LocalDateTime.ToString("dd MMM yyyy · HH:mm");

    public string QualityText =>
        string.IsNullOrWhiteSpace(Quality) ? Format : $"{Format} · {Quality}";
}
