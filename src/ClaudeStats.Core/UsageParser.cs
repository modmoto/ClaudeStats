using System.Globalization;
using System.Text.Json;

namespace ClaudeStats.Core;

/// <summary>
/// Liest das JSON, das Claude Code an die Statusline übergibt
/// (Felder <c>rate_limits.five_hour</c> und <c>rate_limits.seven_day</c>).
/// </summary>
public static class UsageParser
{
    /// <returns>
    /// Einen Snapshot (die Fenster sind null, wenn <c>rate_limits</c> fehlt, z. B. vor der ersten API-Antwort)
    /// oder null, wenn der Text kein gültiges JSON-Objekt ist.
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

    // resets_at kommt je nach Version als Unix-Zeit (Sekunden) oder als ISO-8601-Text.
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
