using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

/// <summary>
/// RefTest subscriptions for real-time updates
/// </summary>
[SubscriptionType]
public static class RefTestSubscriptions
{
    /// <summary>
    /// Subscribe to start events for a specific RefTest
    /// </summary>
    /// <param name="id"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    [Subscribe]
    [Topic("{id}")]
    public static RefTestStarted RefTestStarted(
        [ID<RefTest>] Guid id,
        [EventMessage] RefTestStartedEvent message) => 
        new RefTestStarted(message.Id, message.Status, message.StartedAt);
    
    /// <summary>
    /// Subscribe to completion events for a specific RefTest
    /// </summary>
    /// <param name="id"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    [Subscribe]
    [Topic("{id}")]
    public static RefTestCompleted RefTestCompleted(
        [ID<RefTest>] Guid id,
        [EventMessage] RefTestCompletedEvent message) => 
        new RefTestCompleted(message.Id, message.Status, message.CompletedAt);
    
    /// <summary>
    /// Subscribe to expiration events for a specific RefTest
    /// </summary>
    /// <param name="id"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    [Subscribe]
    [Topic("{id}")]
    public static RefTestExpired RefTestExpired(
        [ID<RefTest>] Guid id,
        [EventMessage] RefTestExpiredEvent message) => 
        new RefTestExpired(message.Id, message.Status, message.ExpiredAt);
    
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
        [EventMessage] RefTestTimeExtendedEvent message) => 
        new RefTestTimeExtended(message.Id, message.NewMaxTimeInMinutes, message.AdditionalMinutes, message.ExtendedAt);

    /// <summary>
    /// Subscribe to invitation sent events for a specific RefTest
    /// </summary>
    /// <param name="id"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    [Subscribe]
    [Topic("{id}")]
    public static RefTestInvitationSent RefTestInvitationSent(
        [ID<RefTest>] Guid id,
        [EventMessage] RefTestInvitationSentEvent message) => 
        new RefTestInvitationSent(message.Id, message.SentAt);

    /// <summary>
    /// Subscribe to result sent events for a specific RefTest
    /// </summary>
    /// <param name="id"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    [Subscribe]
    [Topic("{id}")]
    public static RefTestResultSent RefTestResultSent(
        [ID<RefTest>] Guid id,
        [EventMessage] RefTestResultSentEvent message) => 
        new RefTestResultSent(message.Id, message.SentAt);
}

