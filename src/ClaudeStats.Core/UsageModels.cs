namespace ClaudeStats.Core;

/// <summary>One limit window (5 hours or 7 days) as reported by Claude Code.</summary>
public sealed record RateWindow(double UsedPercentage, DateTimeOffset? ResetsAt)
{
    /// <summary>True if the reset time has already passed since the last snapshot.</summary>
    public bool HasReset(DateTimeOffset now) => ResetsAt is { } r && r <= now;

    /// <summary>
    /// Remaining quota in percent (100 = full). If the reset time has already passed,
    /// the window is assumed to be full again.
    /// </summary>
    public double RemainingPercentage(DateTimeOffset now) =>
        HasReset(now) ? 100 : Math.Clamp(100 - UsedPercentage, 0, 100);

    /// <summary>Time until the next reset, or null if unknown or already passed.</summary>
    public TimeSpan? TimeUntilReset(DateTimeOffset now) =>
        ResetsAt is { } r && r > now ? r - now : null;
}

/// <summary>Last known state of the limits.</summary>
/// <param name="UpdatedAt">When Claude Code last wrote the data.</param>
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
    /// <summary>Green from 30 % remaining, yellow from 10 %, red below that.</summary>
    public static UsageLevel For(double? remainingPercentage) => remainingPercentage switch
    {
        null => UsageLevel.Unknown,
        < 10 => UsageLevel.Critical,
        < 30 => UsageLevel.Low,
        _ => UsageLevel.Ok,
    };
}
