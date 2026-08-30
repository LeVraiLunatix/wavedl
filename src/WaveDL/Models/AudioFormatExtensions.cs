namespace WaveDL.Models;

public static class AudioFormatExtensions
{
    public static string ToYtDlpAudioFormat(this AudioFormat f) => f switch
    {
        AudioFormat.Mp3 => "mp3",
        AudioFormat.Flac => "flac",
        AudioFormat.Wav => "wav",
        AudioFormat.Aac => "aac",
        _ => "mp3",
    };

    /// <summary>Target bitrate passed to <c>--audio-quality</c>. Null for lossless formats.</summary>
    public static string? ToYtDlpAudioQuality(this AudioFormat f) => f switch
    {
        AudioFormat.Mp3 => "320K",
        AudioFormat.Aac => "256K",
        _ => null,
    };

    public static string ToFileExtension(this AudioFormat f) => f switch
    {
        AudioFormat.Mp3 => "mp3",
        AudioFormat.Flac => "flac",
        AudioFormat.Wav => "wav",
        AudioFormat.Aac => "aac",
        _ => "mp3",
    };

    public static bool SupportsEmbeddedArtwork(this AudioFormat f) => f is not AudioFormat.Wav;

    public static string DisplayLabel(this AudioFormat f) => f switch
    {
        AudioFormat.Mp3 => "MP3",
        AudioFormat.Flac => "FLAC",
        AudioFormat.Wav => "WAV",
        AudioFormat.Aac => "AAC",
        _ => "MP3",
    };

    public static string QualityLabel(this AudioFormat f) => f switch
    {
        AudioFormat.Mp3 => "320 kbps",
        AudioFormat.Aac => "256 kbps",
        AudioFormat.Flac => "Lossless",
        AudioFormat.Wav => "PCM",
        _ => string.Empty,
    };

    public static string Describe(this AudioFormat f) => $"{f.DisplayLabel()} · {f.QualityLabel()}";
}

/// <summary>Bindable option for format pickers.</summary>
public sealed record AudioFormatOption(AudioFormat Value, string Label)
{
    public static IReadOnlyList<AudioFormatOption> All { get; } =
    [
        new(AudioFormat.Mp3, "MP3 320 kbps"),
        new(AudioFormat.Flac, "FLAC (lossless)"),
        new(AudioFormat.Wav, "WAV (PCM)"),
        new(AudioFormat.Aac, "AAC 256 kbps"),
    ];

    public static AudioFormatOption From(AudioFormat value) =>
        All.FirstOrDefault(o => o.Value == value) ?? All[0];
}
