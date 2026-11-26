using System.Diagnostics;
using System.Text.Json;

namespace ClaudeStats.Core;

/// <summary>The OAuth access token Claude Code stores after <c>claude</c> login.</summary>
public sealed record OAuthCredentials(string AccessToken, DateTimeOffset? ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } e && e <= now;
}

/// <summary>
/// Reads Claude Code's login. The token is only kept in memory and never written anywhere.
/// <list type="bullet">
/// <item>macOS: Keychain item "Claude Code-credentials"</item>
/// <item>Windows/Linux: <c>~/.claude/.credentials.json</c></item>
/// <item>Any OS: environment variable <c>CLAUDE_CODE_OAUTH_TOKEN</c> (overrides the above)</item>
/// </list>
/// </summary>
public static class CredentialStore
{
    public const string KeychainService = "Claude Code-credentials";

    public static OAuthCredentials? Load()
    {
        var env = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
            return new OAuthCredentials(env.Trim(), null);

        string? json = null;
        if (OperatingSystem.IsMacOS())
            json = ReadKeychain();
        json ??= ReadFile();

        return json is null ? null : Parse(json);
    }

    /// <summary>Parses <c>{"claudeAiOauth":{"accessToken":"...","expiresAt":1764000000000}}</c>.</summary>
    public static OAuthCredentials? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) ||
                oauth.ValueKind != JsonValueKind.Object ||
                !oauth.TryGetProperty("accessToken", out var token) ||
                token.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(token.GetString()))
                return null;

            DateTimeOffset? expiresAt = null;
            if (oauth.TryGetProperty("expiresAt", out var exp))
                expiresAt = UsageParser.ReadTime(exp);

            return new OAuthCredentials(token.GetString()!, expiresAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadFile()
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(configDir))
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

        var path = Path.Combine(configDir, ".credentials.json");
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadKeychain()
    {
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/security")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("find-generic-password");
            psi.ArgumentList.Add("-s");
            psi.ArgumentList.Add(KeychainService);
            psi.ArgumentList.Add("-w");

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            // The first access shows a macOS permission dialog; give the user time to answer it.
            if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
            {
                process.Kill();
                return null;
            }

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
