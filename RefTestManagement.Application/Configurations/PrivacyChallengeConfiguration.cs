using System.ComponentModel.DataAnnotations;

namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>Settings for public privacy challenge verification and its request protections.</summary>
public sealed class PrivacyChallengeConfiguration
{
    /// <summary>Shared lifetime of export and withdrawal verification keys, in hours.</summary>
    [Range(1, 168)]
    public int PrivacyChallengeKeyLifetimeHours { get; init; } = 24;

    /// <summary>Fixed-window length for each operation-scoped public rate limit.</summary>
    [Range(1, int.MaxValue)]
    public int RateLimitWindowSeconds { get; init; } = 60;

    /// <summary>Request challenges allowed per client address in a rate-limit window.</summary>
    [Range(1, int.MaxValue)]
    public int RequestRateLimitPermitLimit { get; init; } = 5;

    /// <summary>Confirmation attempts allowed per client address in a rate-limit window.</summary>
    [Range(1, int.MaxValue)]
    public int ConfirmationRateLimitPermitLimit { get; init; } = 10;

    /// <summary>Rate-limit state backend; Local is only safe for a single API instance.</summary>
    public PrivacyChallengeRateLimitBackend RateLimitBackend { get; init; } =
        PrivacyChallengeRateLimitBackend.Local;

    /// <summary>Authenticated TLS Redis endpoint used outside Development.</summary>
    public string? RedisEndpoint { get; init; }

    /// <summary>Secret used to pseudonymize client addresses in shared limiter keys.</summary>
    public string? HmacSecret { get; init; }

    /// <summary>How often withdrawal batches are reconciled and expired or completed data is cleared, in minutes.</summary>
    [Range(1, int.MaxValue)]
    public int CleanupIntervalMinutes { get; init; } = 15;
}

public enum PrivacyChallengeRateLimitBackend
{
    Local,
    Redis
}
