namespace ClaudeStats.Core;

/// <summary>German UI strings for the menu and tooltip.</summary>
public static class UsageFormatter
{
    public static string Percent(double value) => $"{Math.Round(value):0} %";

    /// <summary>"3 T 4 Std", "2 Std 14 Min" or "12 Min".</summary>
    public static string Countdown(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays} T {span.Hours} Std";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours} Std {span.Minutes} Min";
        return $"{Math.Max(1, (int)Math.Ceiling(span.TotalMinutes))} Min";
    }

    /// <summary>"gerade eben", "vor 5 Min", "vor 2 Std", "vor 3 T".</summary>
    public static string Age(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "gerade eben";
        if (span.TotalHours < 1) return $"vor {(int)span.TotalMinutes} Min";
        if (span.TotalDays < 1) return $"vor {(int)span.TotalHours} Std";
        return $"vor {(int)span.TotalDays} T";
    }

    /// <summary>One menu line, e.g. "5-Stunden-Limit: 68 % übrig · Reset in 2 Std 14 Min".</summary>
    public static string Line(string label, RateWindow? window, DateTimeOffset now)
    {
        if (window is null)
            return $"{label}: keine Daten";

        var remaining = Percent(window.RemainingPercentage(now));
        if (window.HasReset(now))
            return $"{label}: {remaining} übrig · zurückgesetzt";

        return window.TimeUntilReset(now) is { } left
            ? $"{label}: {remaining} übrig · Reset in {Countdown(left)}"
            : $"{label}: {remaining} übrig";
    }
}
