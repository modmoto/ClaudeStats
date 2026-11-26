using System.Security;

namespace ClaudeStats;

/// <summary>
/// "Start at login" on macOS via a LaunchAgent in ~/Library/LaunchAgents.
/// Only available when the app runs from a .app bundle.
/// </summary>
internal static class Autostart
{
    private const string Label = "dev.modmoto.claudestats";

    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", Label + ".plist");

    /// <summary>Path of the .app bundle, or null when not running from one (e.g. from Rider).</summary>
    private static string? BundlePath
    {
        get
        {
            var exe = Environment.ProcessPath;
            const string marker = ".app/Contents/MacOS/";
            var index = exe?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
            return index < 0 ? null : exe![..(index + 4)];
        }
    }

    public static bool IsSupported => OperatingSystem.IsMacOS() && BundlePath is not null;

    public static bool IsEnabled => File.Exists(PlistPath);

    public static void SetEnabled(bool enabled)
    {
        if (!IsSupported)
            return;

        if (!enabled)
        {
            File.Delete(PlistPath);
            Log.Info("Autostart disabled");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
        var bundle = SecurityElement.Escape(BundlePath!);
        File.WriteAllText(PlistPath, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key>
              <string>{Label}</string>
              <key>ProgramArguments</key>
              <array>
                <string>/usr/bin/open</string>
                <string>-a</string>
                <string>{bundle}</string>
              </array>
              <key>RunAtLoad</key>
              <true/>
            </dict>
            </plist>
            """);
        Log.Info($"Autostart enabled for {BundlePath}");
    }
}
