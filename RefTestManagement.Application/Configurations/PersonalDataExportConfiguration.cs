namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>Settings for public privacy challenge verification and its request protections.</summary>
public sealed class PersonalDataExportConfiguration
{
    /// <summary>Shared lifetime of export and withdrawal verification keys, in hours.</summary>
    public int PrivacyChallengeKeyLifetimeHours { get; init; } = 24;

    /// <summary>Fixed-window length for each operation-scoped public rate limit.</summary>
    public int RateLimitWindowSeconds { get; init; } = 60;

    /// <summary>Request challenges allowed per client address in a rate-limit window.</summary>
    public int RequestRateLimitPermitLimit { get; init; } = 5;

    /// <summary>Confirmation attempts allowed per client address in a rate-limit window.</summary>
    public int ConfirmationRateLimitPermitLimit { get; init; } = 10;

    /// <summary>How often expired challenge data and completed withdrawal targets are cleared, in minutes.</summary>
    public int CleanupIntervalMinutes { get; init; } = 15;
}
