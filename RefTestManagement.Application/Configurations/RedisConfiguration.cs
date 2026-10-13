namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>
/// Shared Redis used for GraphQL subscription events, participant session leases, and optionally
/// the privacy challenge rate limiter.
/// </summary>
public sealed class RedisConfiguration
{
    /// <summary>
    /// Determines how the host authenticates to Redis. Token authentication is supplied by a
    /// host-specific adapter so the application configuration remains cloud-provider-neutral.
    /// </summary>
    public RedisAuthenticationMode AuthenticationMode { get; init; } = RedisAuthenticationMode.Password;

    /// <summary>
    /// TLS endpoint. Password mode uses <c>rediss://:password@host:port</c>; token mode uses
    /// <c>rediss://host:port</c>. Leave empty when shared state and the Redis privacy challenge
    /// limiter are not selected.
    /// </summary>
    public string? Endpoint { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>
    /// Rejects an endpoint that does not match the configured TLS authentication mode.
    /// </summary>
    public void Validate()
    {
        if (!Enum.IsDefined(AuthenticationMode))
            throw new InvalidOperationException(
                "RedisConfiguration:AuthenticationMode must be Password or Token.");

        if (!IsConfigured)
            return;

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "rediss"
            || string.IsNullOrWhiteSpace(endpoint.Host)
            || endpoint.Port is < 1 or > 65535)
            throw new InvalidOperationException(
                "RedisConfiguration:Endpoint must be a TLS URI with a host and port.");

        var hasPassword = endpoint.UserInfo.Split(':', 2) is [_, var password]
            && !string.IsNullOrWhiteSpace(password);
        var hasUserInfo = !string.IsNullOrWhiteSpace(endpoint.UserInfo);

        if ((AuthenticationMode == RedisAuthenticationMode.Password && !hasPassword) ||
            (AuthenticationMode == RedisAuthenticationMode.Token && hasUserInfo))
        {
            var expectedFormat = AuthenticationMode == RedisAuthenticationMode.Password
                ? "rediss://:password@host:port"
                : "rediss://host:port without credentials";
            throw new InvalidOperationException(
                $"RedisConfiguration:Endpoint must match the {AuthenticationMode} format ({expectedFormat}).");
        }
    }
}

public enum RedisAuthenticationMode
{
    Password,
    Token
}
