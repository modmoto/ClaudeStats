namespace ClaudeStats.Core;

/// <summary>Locates and reads the snapshot file written by the status line script.</summary>
public static class UsageFile
{
    public const string FileName = "usage-snapshot.json";

    /// <summary>
    /// <c>~/.claude/usage-snapshot.json</c>, or <c>%USERPROFILE%\.claude\usage-snapshot.json</c> on Windows.
    /// Honors <c>CLAUDE_CONFIG_DIR</c>.
    /// </summary>
    public static string DefaultPath()
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(configDir))
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        return Path.Combine(configDir, FileName);
    }

    /// <returns>The snapshot, or null if the file is missing, being replaced right now, or not valid JSON.</returns>
    public static UsageSnapshot? Load(string? path = null)
    {
        path ??= DefaultPath();
        try
        {
            if (!File.Exists(path))
                return null;

            var updatedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            return UsageParser.Parse(File.ReadAllText(path), updatedAt);
        }
        catch (IOException)
        {
            return null; // The script is writing right now; try again on the next tick.
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
