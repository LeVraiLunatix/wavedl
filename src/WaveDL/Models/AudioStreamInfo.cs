namespace WaveDL.Models;

/// <summary>A single audio-only stream advertised by yt-dlp for a given URL.</summary>
public sealed class AudioStreamInfo
{
    public required string FormatId { get; init; }
    public required string Extension { get; init; }
    public string? Codec { get; init; }
    public double? BitrateKbps { get; init; }
    public long? FileSizeBytes { get; init; }

    public string DisplayName
    {
        get
        {
            var codec = string.IsNullOrWhiteSpace(Codec) ? Extension.ToUpperInvariant() : Codec!.ToUpperInvariant();
            var bitrate = BitrateKbps is > 0 ? $"{Math.Round(BitrateKbps.Value)} kbps" : "débit variable";
            return $"{codec} · {bitrate}";
        }
    }
}
