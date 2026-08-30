using System.Text.Json;
using WaveDL.Models;

namespace WaveDL.Services.YtDlp;

/// <summary>Selective parsing of yt-dlp's <c>-J</c> output into WaveDL domain models.</summary>
internal static class YtDlpParser
{
    public static Track ParseTrack(JsonElement info, bool music)
    {
        var id = GetString(info, "id") ?? string.Empty;
        var title = FirstNonEmpty(GetString(info, "track"), GetString(info, "title")) ?? "Titre inconnu";
        var artist = FirstNonEmpty(
                         GetString(info, "artist"),
                         GetString(info, "creator"),
                         GetString(info, "uploader"),
                         GetString(info, "channel"))
                     ?? "Artiste inconnu";

        return new Track
        {
            Id = id,
            Title = title.Trim(),
            Artist = CleanArtist(artist),
            Album = GetString(info, "album"),
            Duration = TimeSpan.FromSeconds(Math.Max(0, GetDouble(info, "duration") ?? 0)),
            ThumbnailUrl = FirstNonEmpty(GetString(info, "thumbnail"), BestThumbnail(info)),
            SourceUrl = FirstNonEmpty(GetString(info, "webpage_url"), BuildWatchUrl(id, music)) ?? BuildWatchUrl(id, music),
            Provider = music ? "YouTube Music" : "YouTube",
        };
    }

    public static IReadOnlyList<Track> ParseSearchEntries(JsonElement root, bool music)
    {
        if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<Track>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetString(entry, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            results.Add(ParseTrack(entry, music));
        }

        return results;
    }

    public static PlaylistInfo ParsePlaylist(JsonElement root, string requestedUrl, bool music)
    {
        var tracks = ParseSearchEntries(root, music);
        return new PlaylistInfo
        {
            Title = FirstNonEmpty(GetString(root, "title"), "Playlist") ?? "Playlist",
            Url = FirstNonEmpty(GetString(root, "webpage_url"), requestedUrl) ?? requestedUrl,
            ThumbnailUrl = FirstNonEmpty(GetString(root, "thumbnail"), tracks.FirstOrDefault()?.BestThumbnailUrl),
            Tracks = tracks,
        };
    }

    public static IReadOnlyList<AudioStreamInfo> ParseAudioStreams(JsonElement info)
    {
        if (!info.TryGetProperty("formats", out var formats) || formats.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var streams = new List<AudioStreamInfo>();
        foreach (var format in formats.EnumerateArray())
        {
            var acodec = GetString(format, "acodec");
            var vcodec = GetString(format, "vcodec");
            if (string.IsNullOrEmpty(acodec) || acodec == "none" || vcodec is not (null or "none"))
            {
                continue;
            }

            streams.Add(new AudioStreamInfo
            {
                FormatId = GetString(format, "format_id") ?? "?",
                Extension = GetString(format, "ext") ?? "?",
                Codec = acodec,
                BitrateKbps = GetDouble(format, "abr") ?? GetDouble(format, "tbr"),
                FileSizeBytes = GetLong(format, "filesize") ?? GetLong(format, "filesize_approx"),
            });
        }

        return streams
            .OrderByDescending(s => s.BitrateKbps ?? 0)
            .ThenByDescending(s => s.FileSizeBytes ?? 0)
            .ToList();
    }

    private static string BuildWatchUrl(string id, bool music) =>
        music ? $"https://music.youtube.com/watch?v={id}" : $"https://www.youtube.com/watch?v={id}";

    private static string CleanArtist(string artist)
    {
        var trimmed = artist.Trim();
        return trimmed.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^" - Topic".Length].Trim()
            : trimmed;
    }

    private static string? BestThumbnail(JsonElement info)
    {
        if (!info.TryGetProperty("thumbnails", out var thumbs) || thumbs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? best = null;
        var bestArea = -1L;
        foreach (var thumb in thumbs.EnumerateArray())
        {
            var url = GetString(thumb, "url");
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var area = (GetLong(thumb, "width") ?? 0) * (GetLong(thumb, "height") ?? 0);
            if (area >= bestArea)
            {
                bestArea = area;
                best = url;
            }
        }

        return best;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static long? GetLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var l) => l,
            JsonValueKind.Number => (long)value.GetDouble(),
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }
}
