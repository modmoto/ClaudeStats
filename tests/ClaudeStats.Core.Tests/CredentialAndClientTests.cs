using System.Net;
using ClaudeStats.Core;
using Xunit;

namespace ClaudeStats.Core.Tests;

public class CredentialStoreTests
{
    [Fact]
    public void Parses_claude_code_credentials()
    {
        var json = """
            {"claudeAiOauth":{"accessToken":"sk-ant-oat01-abc","refreshToken":"r","expiresAt":1764158400000,
             "scopes":["user:inference","user:profile"],"subscriptionType":"max"}}
            """;

        var creds = CredentialStore.Parse(json);

        Assert.Equal("sk-ant-oat01-abc", creds!.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1764158400000), creds.ExpiresAt);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"claudeAiOauth":{}}""")]
    [InlineData("""{"claudeAiOauth":{"accessToken":""}}""")]
    [InlineData("garbage")]
    public void Missing_token_gives_null(string json) =>
        Assert.Null(CredentialStore.Parse(json));

    [Fact]
    public void Expiry_check()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(new OAuthCredentials("t", now.AddSeconds(-1)).IsExpired(now));
        Assert.False(new OAuthCredentials("t", now.AddHours(1)).IsExpired(now));
        Assert.False(new OAuthCredentials("t", null).IsExpired(now));
    }
}

public class UsageClientTests
{
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static readonly OAuthCredentials Valid = new("token-123", DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public async Task Ok_response_gives_snapshot_and_sends_auth_headers()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"five_hour":{"utilization":40,"resets_at":null}}""");
        var client = new UsageClient(new HttpClient(handler)) { LoadCredentials = () => Valid };

        var result = await client.FetchAsync();

        Assert.Equal(FetchStatus.Ok, result.Status);
        Assert.Equal(40, result.Snapshot!.FiveHour!.UsedPercentage);
        Assert.Equal("Bearer token-123", handler.LastRequest!.Headers.Authorization!.ToString());
        Assert.Equal("oauth-2025-04-20", handler.LastRequest.Headers.GetValues("anthropic-beta").Single());
        Assert.Equal(UsageClient.Endpoint, handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task No_credentials_means_not_logged_in()
    {
        var client = new UsageClient(new HttpClient(new FakeHandler(HttpStatusCode.OK, "{}"))) { LoadCredentials = () => null };
        Assert.Equal(FetchStatus.NotLoggedIn, (await client.FetchAsync()).Status);
    }

    [Fact]
    public async Task Expired_token_is_not_sent()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var client = new UsageClient(new HttpClient(handler))
        {
            LoadCredentials = () => new OAuthCredentials("old", DateTimeOffset.UtcNow.AddMinutes(-5)),
        };

        Assert.Equal(FetchStatus.TokenExpired, (await client.FetchAsync()).Status);
        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, FetchStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, FetchStatus.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, FetchStatus.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, FetchStatus.Failed)]
    public async Task Error_codes_are_mapped(HttpStatusCode code, FetchStatus expected)
    {
        var client = new UsageClient(new HttpClient(new FakeHandler(code, "nope"))) { LoadCredentials = () => Valid };
        Assert.Equal(expected, (await client.FetchAsync()).Status);
    }
}
