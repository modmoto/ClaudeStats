namespace ClaudeStats.Core;

/// <summary>Findet und liest die von der Statusline geschriebene Snapshot-Datei.</summary>
public static class UsageFile
{
    public const string FileName = "usage-snapshot.json";

    /// <summary>
    /// <c>~/.claude/usage-snapshot.json</c> bzw. unter Windows <c>%USERPROFILE%\.claude\usage-snapshot.json</c>.
    /// Respektiert <c>CLAUDE_CONFIG_DIR</c>.
    /// </summary>
    public static string DefaultPath()
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(configDir))
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        return Path.Combine(configDir, FileName);
    }

    /// <returns>Den Snapshot, oder null wenn die Datei fehlt, gerade ersetzt wird oder kein gültiges JSON enthält.</returns>
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
            return null; // Skript schreibt gerade; beim nächsten Tick erneut versuchen.
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
