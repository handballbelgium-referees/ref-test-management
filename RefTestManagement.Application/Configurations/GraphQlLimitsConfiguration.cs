namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>
/// Limits protecting the GraphQL endpoint from abuse. Both the participant test-taking flow and
/// the endpoint's introspection are reachable without signing in, so the endpoint has to defend
/// itself rather than rely on authorization alone.
/// </summary>
public class GraphQlLimitsConfiguration
{
    /// <summary>
    /// Whether HotChocolate rejects queries whose analysed cost exceeds the limits below.
    /// Turn this off only to diagnose a legitimate query being rejected.
    /// </summary>
    public bool EnforceCostLimits { get; init; } = true;

    /// <summary>
    /// Maximum analysed field cost. Field cost grows with the number of resolvers a query asks
    /// for, including every filter and sort argument it can use, so this bounds work like deeply
    /// repeated selections and oversized inline filter trees.
    /// </summary>
    /// <remarks>
    /// Measured against the real schema: the admin ref test list with its full selection, filter
    /// and sort costs 3,667; the audit log page costs 1,122; a ref test with its questions and
    /// answers costs 43. The limit leaves roughly five times headroom over the heaviest
    /// legitimate query while still rejecting amplification, which HotChocolate's default of
    /// 1,000,000 would not.
    /// </remarks>
    public double MaxFieldCost { get; init; } = 20_000;

    /// <summary>
    /// Maximum analysed type cost, which grows with the number of objects a query can return —
    /// page size multiplied through every nested list.
    /// </summary>
    /// <remarks>
    /// Measured against the real schema: a 100-item page of ref tests with their titles costs
    /// 303 and the audit log page costs 203, so the HotChocolate default of 1,000 is closer to
    /// legitimate traffic than is comfortable. This leaves roughly sixteen times headroom for
    /// future list queries without allowing pathological nesting.
    /// </remarks>
    public double MaxTypeCost { get; init; } = 5_000;

    /// <summary>
    /// Requests allowed per client address within <see cref="RateLimitWindowSeconds"/>. Sized for
    /// the test-taking UI, which saves progress on a 500ms debounce and polls nothing else.
    /// </summary>
    public int RateLimitPermitLimit { get; init; } = 300;

    /// <summary>Length of the rate limit window, in seconds.</summary>
    public int RateLimitWindowSeconds { get; init; } = 60;

    /// <summary>
    /// Requests queued once the limit is reached, instead of being rejected outright. A small
    /// queue absorbs the bursts a single participant's UI produces without masking real abuse.
    /// </summary>
    public int RateLimitQueueLimit { get; init; } = 20;

    /// <summary>
    /// Whether the rate limiter is applied at all. Disable it when the deployment already rate
    /// limits at a reverse proxy, which sees the true client address.
    /// </summary>
    public bool EnableRateLimiting { get; init; } = true;
}
