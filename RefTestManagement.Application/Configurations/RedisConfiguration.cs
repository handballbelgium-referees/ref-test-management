namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>
/// Shared Redis used for GraphQL subscription events, participant session leases, and optionally
/// the privacy challenge rate limiter.
/// </summary>
public sealed class RedisConfiguration
{
    /// <summary>
    /// Authenticated TLS endpoint (<c>rediss://:password@host:port</c>). Leave empty when shared
    /// state and the Redis privacy challenge limiter are not selected.
    /// </summary>
    public string? Endpoint { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>
    /// Rejects an endpoint that is not authenticated TLS.
    /// </summary>
    public void Validate()
    {
        if (!IsConfigured)
            return;

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "rediss"
            || endpoint.Port is < 1 or > 65535
            || endpoint.UserInfo.Split(':', 2) is not [_, var password]
            || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "RedisConfiguration:Endpoint must be an authenticated TLS URI (rediss://:password@host:port).");
    }
}
