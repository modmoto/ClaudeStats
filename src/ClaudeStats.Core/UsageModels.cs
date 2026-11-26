namespace ClaudeStats.Core;

/// <summary>Ein Limit-Fenster (5 Stunden oder 7 Tage) so, wie Claude Code es meldet.</summary>
public sealed record RateWindow(double UsedPercentage, DateTimeOffset? ResetsAt)
{
    /// <summary>True, wenn der Reset-Zeitpunkt seit dem letzten Snapshot bereits verstrichen ist.</summary>
    public bool HasReset(DateTimeOffset now) => ResetsAt is { } r && r <= now;

    /// <summary>
    /// Verbleibendes Kontingent in Prozent (100 = voll). Ist der Reset-Zeitpunkt schon vorbei,
    /// wird angenommen, dass das Fenster wieder voll ist.
    /// </summary>
    public double RemainingPercentage(DateTimeOffset now) =>
        HasReset(now) ? 100 : Math.Clamp(100 - UsedPercentage, 0, 100);

    /// <summary>Zeit bis zum nächsten Reset, oder null wenn unbekannt bzw. bereits vorbei.</summary>
    public TimeSpan? TimeUntilReset(DateTimeOffset now) =>
        ResetsAt is { } r && r > now ? r - now : null;
}

/// <summary>Letzter bekannter Stand der Limits.</summary>
/// <param name="UpdatedAt">Zeitpunkt, zu dem Claude Code die Daten zuletzt geschrieben hat.</param>
public sealed record UsageSnapshot(RateWindow? FiveHour, RateWindow? SevenDay, DateTimeOffset UpdatedAt);

public enum UsageLevel
{
    Unknown,
    Ok,
    Low,
    Critical,
}

public static class UsageLevels
{
    /// <summary>Grün ab 30 % Rest, Gelb ab 10 %, darunter Rot.</summary>
    public static UsageLevel For(double? remainingPercentage) => remainingPercentage switch
    {
        null => UsageLevel.Unknown,
        < 10 => UsageLevel.Critical,
        < 30 => UsageLevel.Low,
        _ => UsageLevel.Ok,
    };
}
