using System.Globalization;

namespace WaveDL.Helpers;

/// <summary>Human-readable formatting for sizes, speeds and durations.</summary>
public static class Humanize
{
    private static readonly string[] SizeUnits = ["o", "Ko", "Mo", "Go", "To"];

    public static string Bytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 o";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < SizeUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{value:0.#} {SizeUnits[unit]}");
    }

    public static string Speed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "—";
        }

        return $"{Bytes((long)bytesPerSecond)}/s";
    }

    public static string Eta(TimeSpan eta)
    {
        if (eta <= TimeSpan.Zero)
        {
            return "—";
        }

        if (eta.TotalHours >= 1)
        {
            return $"{(int)eta.TotalHours} h {eta.Minutes:00} min";
        }

        if (eta.TotalMinutes >= 1)
        {
            return $"{eta.Minutes} min {eta.Seconds:00} s";
        }

        return $"{eta.Seconds} s";
    }

    public static string Duration(TimeSpan duration) => duration.TotalHours >= 1
        ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
