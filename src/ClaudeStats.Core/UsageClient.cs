using System.Net;
using System.Net.Http.Headers;

namespace ClaudeStats.Core;

public enum FetchStatus
{
    Ok,
    NotLoggedIn,
    TokenExpired,
    Unauthorized,
    RateLimited,
    Failed,
}

public sealed record FetchResult(FetchStatus Status, UsageSnapshot? Snapshot = null, string? Detail = null, TimeSpan? RetryAfter = null);

/// <summary>
/// Fetches the plan usage from Anthropic with Claude Code's login.
/// This endpoint is not officially documented; it is what Claude Code's <c>/usage</c> uses.
/// </summary>
public sealed class UsageClient(HttpClient http)
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    public Func<OAuthCredentials?> LoadCredentials { get; init; } = CredentialStore.Load;

    public async Task<FetchResult> FetchAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var credentials = LoadCredentials();
        if (credentials is null)
            return new FetchResult(FetchStatus.NotLoggedIn);
        if (credentials.IsExpired(now))
            return new FetchResult(FetchStatus.TokenExpired);

        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.UserAgent.ParseAdd("ClaudeStats/0.2");
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    return new FetchResult(FetchStatus.Unauthorized, Detail: $"HTTP {(int)response.StatusCode}");
                case HttpStatusCode.TooManyRequests:
                    return new FetchResult(FetchStatus.RateLimited,
                        Detail: "HTTP 429",
                        RetryAfter: response.Headers.RetryAfter?.Delta);
            }

            if (!response.IsSuccessStatusCode)
                return new FetchResult(FetchStatus.Failed, Detail: $"HTTP {(int)response.StatusCode}: {Truncate(body)}");

            var snapshot = UsageParser.Parse(body, now);
            return snapshot is null
                ? new FetchResult(FetchStatus.Failed, Detail: $"Unexpected response: {Truncate(body)}")
                : new FetchResult(FetchStatus.Ok, snapshot);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FetchResult(FetchStatus.Failed, Detail: "Timeout");
        }
        catch (HttpRequestException e)
        {
            return new FetchResult(FetchStatus.Failed, Detail: e.Message);
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";
}
