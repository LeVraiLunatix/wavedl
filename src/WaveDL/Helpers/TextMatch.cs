using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WaveDL.Helpers;

/// <summary>String normalization and fuzzy similarity used to score YouTube matches.</summary>
public static partial class TextMatch
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = RemoveDiacritics(value.ToLowerInvariant());
        var sb = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return WhitespaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    /// <summary>Normalized edit-distance ratio in [0, 1].</summary>
    public static double Ratio(string? a, string? b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        if (na.Length == 0 && nb.Length == 0)
        {
            return 1;
        }

        if (na.Length == 0 || nb.Length == 0)
        {
            return 0;
        }

        var distance = Levenshtein(na, nb);
        return 1.0 - ((double)distance / Math.Max(na.Length, nb.Length));
    }

    /// <summary>Order-insensitive similarity combining Jaccard on tokens and a sequence ratio.</summary>
    public static double TokenSetRatio(string? a, string? b)
    {
        var ta = new SortedSet<string>(Normalize(a).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var tb = new SortedSet<string>(Normalize(b).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (ta.Count == 0 && tb.Count == 0)
        {
            return 1;
        }

        if (ta.Count == 0 || tb.Count == 0)
        {
            return 0;
        }

        var intersection = new SortedSet<string>(ta);
        intersection.IntersectWith(tb);
        var union = new SortedSet<string>(ta);
        union.UnionWith(tb);

        var jaccard = (double)intersection.Count / union.Count;
        var sequence = Ratio(string.Join(' ', ta), string.Join(' ', tb));
        return (0.5 * jaccard) + (0.5 * sequence);
    }

    public static bool ContainsAny(string? haystack, params string[] needles)
    {
        var normalized = Normalize(haystack);
        foreach (var needle in needles)
        {
            if (normalized.Contains(Normalize(needle), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
