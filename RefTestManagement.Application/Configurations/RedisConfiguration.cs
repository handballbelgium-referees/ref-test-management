namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>
/// Shared Redis used once the API runs as more than one replica: GraphQL subscription events, the
/// participant session lease and, when selected, the privacy challenge rate limiter.
/// </summary>
public sealed class RedisConfiguration
{
    /// <summary>
    /// Authenticated TLS endpoint (<c>rediss://:password@host:port</c>). Leave empty for development
    /// and single-instance hosting, where all of the above stays in-process.
    /// </summary>
    public string? Endpoint { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>
    /// Rejects an endpoint that is not authenticated TLS, and a missing endpoint on Azure Container
    /// Apps (identified by <paramref name="containerAppName"/>), which runs several replicas.
    /// </summary>
    public void Validate(string? containerAppName)
    {
        if (!IsConfigured)
        {
            if (!string.IsNullOrWhiteSpace(containerAppName))
                throw new InvalidOperationException(
                    "Azure Container Apps requires RedisConfiguration:Endpoint, because state shared by replicas lives in Redis.");
            return;
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "rediss"
            || endpoint.Port is < 1 or > 65535
            || endpoint.UserInfo.Split(':', 2) is not [_, var password]
            || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "RedisConfiguration:Endpoint must be an authenticated TLS URI (rediss://:password@host:port).");
    }
}
