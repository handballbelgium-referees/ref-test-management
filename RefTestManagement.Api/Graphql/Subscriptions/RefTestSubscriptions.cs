using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

/// <summary>
/// RefTest subscriptions for real-time updates
/// </summary>
[SubscriptionType]
public static class RefTestSubscriptions
{
    /// <summary>
    /// Subscribe to time extension events for a specific RefTest
    /// </summary>
    /// <param name="id">The ID of the RefTest to monitor</param>
    /// <param name="message">The time extension event message</param>
    /// <returns></returns>
    [Subscribe]
    [Topic("{id}")]
    public static RefTestTimeExtended RefTestTimeExtended(
        [ID<RefTest>] Guid id,
        [EventMessage] RefTestTimeExtended message) => message;
}
