using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Api.Identity;

/// <summary>
/// Exchanges a refresh token at the identity provider token endpoint.
/// A rotated refresh token can be redeemed only once, so concurrent and immediately repeated requests that present
/// the same refresh token share one exchange. The shared result lives in this process only for a short time, so
/// replicas do not share it.
/// </summary>
internal sealed class OidcTokenRefresher : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SharedResultLifetime = TimeSpan.FromSeconds(60);

    private readonly ILogger<OidcTokenRefresher> _logger;
    private readonly TimeSpan _timeout;
    private readonly MemoryCache _results = new(new MemoryCacheOptions { SizeLimit = 1024 });
    private readonly ConcurrentDictionary<string, Task<OidcTokenRefreshResult>> _inflight = new(StringComparer.Ordinal);

    public OidcTokenRefresher(ILogger<OidcTokenRefresher> logger)
        : this(logger, DefaultTimeout)
    {
    }

    internal OidcTokenRefresher(ILogger<OidcTokenRefresher> logger, TimeSpan timeout)
    {
        _logger = logger;
        _timeout = timeout;
    }

    public async Task<OidcTokenRefreshResult> RefreshAsync(
        OpenIdConnectOptions options,
        IdentityProvider provider,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        // Key by a hash so the refresh token itself is never held as a dictionary key.
        var key = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        if (_results.TryGetValue(key, out OidcTokenRefreshResult? shared) && shared is not null)
            return shared;

        // The shared exchange ignores the caller's token so one cancelled request cannot fail the others.
        var task = _inflight.GetOrAdd(key, _ => ExchangeOnceAsync(key, options, provider, refreshToken));
        _ = task.ContinueWith(
            _ => _inflight.TryRemove(new KeyValuePair<string, Task<OidcTokenRefreshResult>>(key, task)),
            TaskScheduler.Default);

        return await task.WaitAsync(cancellationToken);
    }

    public void Dispose() => _results.Dispose();

    private async Task<OidcTokenRefreshResult> ExchangeOnceAsync(
        string key,
        OpenIdConnectOptions options,
        IdentityProvider provider,
        string refreshToken)
    {
        var result = await ExchangeAsync(options, provider, refreshToken);
        if (result.Succeeded)
        {
            _results.Set(key, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SharedResultLifetime,
                Size = 1,
            });
        }

        return result;
    }

    private async Task<OidcTokenRefreshResult> ExchangeAsync(
        OpenIdConnectOptions options,
        IdentityProvider provider,
        string refreshToken)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        try
        {
            var tokenEndpoint = await ResolveTokenEndpointAsync(options, provider, timeout.Token);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = options.ClientId ?? string.Empty,
                ["refresh_token"] = refreshToken,
            };
            if (!string.IsNullOrEmpty(options.ClientSecret))
                form["client_secret"] = options.ClientSecret;

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form),
            };
            using var response = await options.Backchannel.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var error = ReadErrorCode(body);
                OidcTokenRefresherLog.RefreshRejected(_logger, (int)response.StatusCode, error);
                return OidcTokenRefreshResult.Failed(error);
            }

            var result = ParseTokens(body, refreshToken);
            if (result.Succeeded)
                OidcTokenRefresherLog.RefreshSucceeded(_logger);
            else
                OidcTokenRefresherLog.RefreshFailed(_logger, result.FailureReason ?? "unknown");
            return result;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            OidcTokenRefresherLog.RefreshFailed(_logger, "timeout");
            return OidcTokenRefreshResult.Failed("timeout");
        }
        catch (Exception exception)
        {
            // Fail closed: any transport or parsing problem ends the refresh attempt.
            OidcTokenRefresherLog.RefreshFailed(_logger, exception.GetType().Name);
            return OidcTokenRefreshResult.Failed(exception.GetType().Name);
        }
    }

    private static async Task<string> ResolveTokenEndpointAsync(
        OpenIdConnectOptions options,
        IdentityProvider provider,
        CancellationToken cancellationToken)
    {
        if (options.ConfigurationManager is not null)
        {
            var configuration = await options.ConfigurationManager.GetConfigurationAsync(cancellationToken);
            if (!string.IsNullOrEmpty(configuration.TokenEndpoint))
                return configuration.TokenEndpoint;
        }

        // Fallback used only when discovery is unavailable. Both paths are standard for their provider.
        var authority = (options.Authority ?? string.Empty).TrimEnd('/');
        return provider == IdentityProvider.Keycloak
            ? $"{authority}/protocol/openid-connect/token"
            : $"{authority}/oauth/token";
    }

    private static OidcTokenRefreshResult ParseTokens(string body, string currentRefreshToken)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("access_token", out var accessElement)
            || accessElement.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(accessElement.GetString()))
        {
            return OidcTokenRefreshResult.Failed("missing_access_token");
        }

        if (!root.TryGetProperty("expires_in", out var expiresElement) || !expiresElement.TryGetInt32(out var expiresIn))
            return OidcTokenRefreshResult.Failed("missing_expires_in");

        // Refresh-token rotation returns a new token; without rotation the current one stays valid.
        var refreshToken = root.TryGetProperty("refresh_token", out var refreshElement)
            && refreshElement.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(refreshElement.GetString())
                ? refreshElement.GetString()
                : currentRefreshToken;

        var idToken = root.TryGetProperty("id_token", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()
            : null;

        return new OidcTokenRefreshResult(
            Succeeded: true,
            AccessToken: accessElement.GetString(),
            IdToken: idToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            FailureReason: null);
    }

    private static string ReadErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString() ?? "unknown"
                : "unknown";
        }
        catch (JsonException)
        {
            return "unparseable";
        }
    }
}

/// <summary>
/// Outcome of a refresh-token exchange. Tokens are never printed: <see cref="ToString"/> reports only the outcome.
/// </summary>
internal sealed record OidcTokenRefreshResult(
    bool Succeeded,
    string? AccessToken,
    string? IdToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string? FailureReason)
{
    public static OidcTokenRefreshResult Failed(string reason) => new(false, null, null, null, default, reason);

    public override string ToString() => Succeeded ? "refresh succeeded" : $"refresh failed: {FailureReason}";
}

internal static partial class OidcTokenRefresherLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Refresh token exchanged; access token renewed.")]
    public static partial void RefreshSucceeded(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Identity provider rejected the refresh token. Status {StatusCode}, error {Error}.")]
    public static partial void RefreshRejected(ILogger logger, int statusCode, string error);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Refresh token exchange failed: {Reason}.")]
    public static partial void RefreshFailed(ILogger logger, string reason);
}
