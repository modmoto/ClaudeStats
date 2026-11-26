namespace ClaudeStats;

/// <summary>
/// Minimal file log for troubleshooting.
/// macOS: ~/Library/Logs/ClaudeStats/ClaudeStats.log, Windows: %LOCALAPPDATA%\ClaudeStats\ClaudeStats.log.
/// Never logs tokens.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string FilePath { get; } = ResolvePath();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? e = null) =>
        Write("ERROR", e is null ? message : $"{message}: {e}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 1_000_000)
                    File.WriteAllText(FilePath, string.Empty);

                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static string ResolvePath()
    {
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Logs", "ClaudeStats", "ClaudeStats.log");
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "ClaudeStats", "ClaudeStats.log");
    }
}
