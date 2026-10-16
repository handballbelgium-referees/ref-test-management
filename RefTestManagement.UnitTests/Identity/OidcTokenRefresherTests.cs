using System.Net;
using System.Net.Http;
using System.Text;
using Handball.Belgium.RefTestManagement.Api.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests.Identity;

public sealed class OidcTokenRefresherTests
{
    private const string Endpoint = "https://tenant.example.test/oauth/token";

    [Fact]
    public async Task SuccessfulExchangeReturnsNewTokensAndKeepsRefreshTokenWhenNotRotated()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"access_token":"new-access","expires_in":300,"id_token":"new-id"}""")));
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance);

        var result = await refresher.RefreshAsync(
            CreateOptions(handler), IdentityProvider.Auth0, "refresh-1", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("new-access", result.AccessToken);
        Assert.Equal("new-id", result.IdToken);
        Assert.Equal("refresh-1", result.RefreshToken);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(200));
        Assert.Equal(Endpoint, handler.LastRequestUri);
        Assert.Contains("grant_type=refresh_token", handler.LastRequestBody);
    }

    [Fact]
    public async Task RotatedRefreshTokenIsReturned()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"access_token":"a","expires_in":300,"refresh_token":"rotated"}""")));
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance);

        var result = await refresher.RefreshAsync(
            CreateOptions(handler), IdentityProvider.Auth0, "refresh-1", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("rotated", result.RefreshToken);
    }

    [Fact]
    public async Task InvalidGrantRejectsTheRefresh()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.BadRequest,
            """{"error":"invalid_grant"}""")));
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance);

        var result = await refresher.RefreshAsync(
            CreateOptions(handler), IdentityProvider.Auth0, "revoked", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_grant", result.FailureReason);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task MissingAccessTokenFailsClosed()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, """{"expires_in":300}""")));
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance);

        var result = await refresher.RefreshAsync(
            CreateOptions(handler), IdentityProvider.Auth0, "refresh-1", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("missing_access_token", result.FailureReason);
    }

    [Fact]
    public async Task TransportFailureFailsClosed()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("network down"));
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance);

        var result = await refresher.RefreshAsync(
            CreateOptions(handler), IdentityProvider.Auth0, "refresh-1", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SlowProviderTimesOut()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json(HttpStatusCode.OK, "{}");
        });
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance, TimeSpan.FromMilliseconds(100));

        var result = await refresher.RefreshAsync(
            CreateOptions(handler), IdentityProvider.Auth0, "refresh-1", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("timeout", result.FailureReason);
    }

    [Fact]
    public async Task ConcurrentRefreshesWithSameTokenShareOneExchange()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var handler = new StubHandler(async (_, _) =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return Json(HttpStatusCode.OK, """{"access_token":"a","expires_in":300}""");
        });
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance);
        var options = CreateOptions(handler);

        var first = refresher.RefreshAsync(options, IdentityProvider.Auth0, "same", CancellationToken.None);
        var second = refresher.RefreshAsync(options, IdentityProvider.Auth0, "same", CancellationToken.None);
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ResultToStringNeverContainsTokens()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"access_token":"SECRET-ACCESS","expires_in":300,"refresh_token":"SECRET-REFRESH"}""")));
        using var refresher = new OidcTokenRefresher(NullLogger<OidcTokenRefresher>.Instance);

        var result = await refresher.RefreshAsync(
            CreateOptions(handler), IdentityProvider.Auth0, "refresh-1", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain("SECRET", result.ToString());
    }

    private static OpenIdConnectOptions CreateOptions(HttpMessageHandler handler) => new()
    {
        Authority = "https://tenant.example.test/",
        ClientId = "client",
        ClientSecret = "secret",
        Backchannel = new HttpClient(handler),
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
            _respond = respond;

        public string? LastRequestUri { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _respond(request, cancellationToken);
        }
    }
}
