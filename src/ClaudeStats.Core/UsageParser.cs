using System.Globalization;
using System.Text.Json;

namespace ClaudeStats.Core;

/// <summary>
/// Parses the response of <c>GET https://api.anthropic.com/api/oauth/usage</c>:
/// <c>{"five_hour":{"utilization":12.0,"resets_at":"..."},"seven_day":{...}}</c>.
/// <c>utilization</c> is a percentage (0–100).
/// </summary>
public static class UsageParser
{
    /// <returns>A snapshot, or null if the text is not a valid JSON object.</returns>
    public static UsageSnapshot? Parse(string json, DateTimeOffset updatedAt)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            return new UsageSnapshot(
                ReadWindow(root, "five_hour"),
                ReadWindow(root, "seven_day"),
                updatedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RateWindow? ReadWindow(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
            return null;

        if (!window.TryGetProperty("utilization", out var used) || used.ValueKind != JsonValueKind.Number)
            return null;

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("resets_at", out var reset))
            resetsAt = ReadTime(reset);

        return new RateWindow(used.GetDouble(), resetsAt);
    }

    // resets_at is normally an ISO 8601 string; Unix time (seconds or milliseconds) is accepted too.
    internal static DateTimeOffset? ReadTime(JsonElement element)
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
