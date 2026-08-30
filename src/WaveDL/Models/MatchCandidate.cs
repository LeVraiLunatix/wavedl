namespace WaveDL.Models;

/// <summary>A YouTube Music result ranked against an <see cref="ExternalTrackInfo"/>.</summary>
public sealed class MatchCandidate
{
    public required Track Track { get; init; }

    /// <summary>0..1 — higher means a more likely match.</summary>
    public required double Confidence { get; init; }

    public int ConfidencePercent => (int)Math.Round(Confidence * 100);

    public string ConfidenceText => $"Confiance {ConfidencePercent} %";

    public string ConfidenceLabel => Confidence switch
    {
        >= 0.85 => "Excellent",
        >= 0.65 => "Bon",
        >= 0.45 => "Moyen",
        _ => "Faible",
    };
}
