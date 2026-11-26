using System.Globalization;
using System.Text.Json;

namespace ClaudeStats.Core;

/// <summary>
/// Reads the JSON that Claude Code passes to the status line
/// (fields <c>rate_limits.five_hour</c> and <c>rate_limits.seven_day</c>).
/// </summary>
public static class UsageParser
{
    /// <returns>
    /// A snapshot (the windows are null if <c>rate_limits</c> is missing, e.g. before the first API response),
    /// or null if the text is not a valid JSON object.
    /// </returns>
    public static UsageSnapshot? Parse(string json, DateTimeOffset updatedAt)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (!root.TryGetProperty("rate_limits", out var limits) || limits.ValueKind != JsonValueKind.Object)
                return new UsageSnapshot(null, null, updatedAt);

            return new UsageSnapshot(
                ReadWindow(limits, "five_hour"),
                ReadWindow(limits, "seven_day"),
                updatedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RateWindow? ReadWindow(JsonElement limits, string name)
    {
        if (!limits.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
            return null;

        if (!window.TryGetProperty("used_percentage", out var used) || used.ValueKind != JsonValueKind.Number)
            return null;

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("resets_at", out var reset))
            resetsAt = ReadTime(reset);

        return new RateWindow(used.GetDouble(), resetsAt);
    }

    // Depending on the version, resets_at is either Unix time (seconds) or an ISO 8601 string.
    private static DateTimeOffset? ReadTime(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetDouble(out var n):
                return n > 1e11
                    ? DateTimeOffset.FromUnixTimeMilliseconds((long)n)
                    : DateTimeOffset.FromUnixTimeSeconds((long)n);

            case JsonValueKind.String when DateTimeOffset.TryParse(
                element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                return parsed;

            default:
                return null;
        }
    }
}
