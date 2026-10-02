namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>Settings for public personal-data export mailbox verification.</summary>
public sealed class PersonalDataExportConfiguration
{
    /// <summary>Lifetime of a verification key, in hours. Defaults to one day.</summary>
    public int KeyLifetimeHours { get; init; } = 24;

    /// <summary>Fixed-window length for each operation-scoped public rate limit.</summary>
    public int RateLimitWindowSeconds { get; init; } = 60;

    /// <summary>Request challenges allowed per client address in a rate-limit window.</summary>
    public int RequestRateLimitPermitLimit { get; init; } = 5;

    /// <summary>Confirmation attempts allowed per client address in a rate-limit window.</summary>
    public int ConfirmationRateLimitPermitLimit { get; init; } = 10;

    /// <summary>How often expired, unverified challenge data is cleared, in minutes.</summary>
    public int CleanupIntervalMinutes { get; init; } = 15;
}
