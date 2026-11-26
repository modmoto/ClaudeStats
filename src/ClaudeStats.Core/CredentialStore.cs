using System.Diagnostics;
using System.Text.Json;

namespace ClaudeStats.Core;

/// <summary>The OAuth access token Claude Code stores after <c>claude</c> login.</summary>
public sealed record OAuthCredentials(string AccessToken, DateTimeOffset? ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } e && e <= now;
}

/// <summary>
/// Reads Claude Code's login. The token is only kept in memory and never written anywhere or logged.
/// <list type="bullet">
/// <item>macOS: Keychain item "Claude Code-credentials" (or a variant starting with that name)</item>
/// <item>Windows/Linux: <c>~/.claude/.credentials.json</c></item>
/// <item>Any OS: environment variable <c>CLAUDE_CODE_OAUTH_TOKEN</c> (overrides the above)</item>
/// </list>
/// </summary>
public static class CredentialStore
{
    public const string KeychainService = "Claude Code-credentials";

    /// <summary>Receives diagnostic messages (never secrets).</summary>
    public static Action<string>? Log { get; set; }

    public static OAuthCredentials? Load()
    {
        var env = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
        {
            Log?.Invoke("Using token from CLAUDE_CODE_OAUTH_TOKEN");
            return new OAuthCredentials(env.Trim(), null);
        }

        if (OperatingSystem.IsMacOS())
        {
            foreach (var service in KeychainServices())
            {
                var fromKeychain = ReadKeychain(service);
                var creds = fromKeychain is null ? null : Parse(fromKeychain);
                if (fromKeychain is not null && creds is null)
                    Log?.Invoke($"Keychain item '{service}' found, but it has no claudeAiOauth.accessToken (length {fromKeychain.Length})");
                if (creds is not null)
                {
                    Log?.Invoke($"Login read from Keychain item '{service}'");
                    return creds;
                }
            }
        }

        var fromFile = ReadFile(out var path);
        var fileCreds = fromFile is null ? null : Parse(fromFile);
        if (fileCreds is not null)
        {
            Log?.Invoke($"Login read from {path}");
            return fileCreds;
        }

        Log?.Invoke("No Claude Code login found (checked Keychain and credentials file)");
        return null;
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

    private static string? ReadFile(out string path)
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(configDir))
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

        path = Path.Combine(configDir, ".credentials.json");
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The default item name first, then any other item whose name starts with it.</summary>
    private static IEnumerable<string> KeychainServices()
    {
        yield return KeychainService;

        foreach (var name in ListKeychainServices())
            if (name != KeychainService)
                yield return name;
    }

    /// <summary>Lists service names only (no secrets, no permission dialog).</summary>
    private static IEnumerable<string> ListKeychainServices()
    {
        var result = RunSecurity(TimeSpan.FromSeconds(20), "dump-keychain");
        if (result is null)
            yield break;

        var found = new List<string>();
        foreach (var line in result.Value.Output.Split('\n'))
        {
            // Lines look like:   "svce"<blob>="Claude Code-credentials"
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("\"svce\"", StringComparison.Ordinal))
                continue;

            var start = trimmed.IndexOf("=\"", StringComparison.Ordinal);
            var end = trimmed.LastIndexOf('"');
            if (start < 0 || end <= start + 2)
                continue;

            var name = trimmed[(start + 2)..end];
            if (name.StartsWith("Claude Code", StringComparison.OrdinalIgnoreCase) && !found.Contains(name))
                found.Add(name);
        }

        Log?.Invoke($"Keychain items starting with 'Claude Code': {(found.Count == 0 ? "none" : string.Join(", ", found))}");
        foreach (var name in found)
            yield return name;
    }

    private static string? ReadKeychain(string service)
    {
        // The first access shows a macOS permission dialog; give the user time to answer it.
        var result = RunSecurity(TimeSpan.FromMinutes(2), "find-generic-password", "-s", service, "-w");
        if (result is null)
            return null;

        var (exitCode, output, error) = result.Value;
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            return output.Trim();

        var hint = exitCode switch
        {
            44 => "item not found",
            36 or 128 => "access denied or dialog cancelled",
            51 => "no user interaction possible",
            _ => "unknown",
        };
        Log?.Invoke($"security find-generic-password '{service}' -> exit {exitCode} ({hint}) {error.Trim()}");
        return null;
    }

    private static (int ExitCode, string Output, string Error)? RunSecurity(TimeSpan timeout, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/security")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            // Read both streams concurrently so a full pipe can never block the process.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeout))
            {
                process.Kill();
                Log?.Invoke($"security {args[0]} timed out");
                return null;
            }

            return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }
        catch (Exception e)
        {
            Log?.Invoke($"security {args[0]} failed: {e.Message}");
            return null;
        }
    }
}
