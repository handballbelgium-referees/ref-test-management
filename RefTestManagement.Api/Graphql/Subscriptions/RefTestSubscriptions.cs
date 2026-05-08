using System.Runtime.CompilerServices;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

/// <summary>
/// RefTest subscriptions for real-time updates
/// </summary>
[SubscriptionType]
public static class RefTestSubscriptions
{
    /// <summary>
    /// Subscribe to time extension events for a specific RefTest.
    /// PUBLIC: No authorization required - test takers need to see time extensions in real-time.
    /// </summary>
    /// <param name="id">The ID of the RefTest to monitor</param>
    /// <param name="message">The time extension event message</param>
    /// <returns></returns>
    [Subscribe]
    [Topic(RefTestSubscriptionService.TimeExtendedTopic)]
    public static RefTestTimeExtended RefTestTimeExtended(
        [ID<RefTestDto>] Guid id,
        [EventMessage] RefTestTimeExtendedEvent message) =>
        new(message.Id, message.NewMaxTimeInMinutes, message.AdditionalMinutes, message.ExtendedAt);

    /// <summary>
    /// Subscribe to status/notification events for a SPECIFIC RefTest (for admin detail views).
    /// AUTHORIZED: Requires authentication - returns RefTestStarted, RefTestCompleted, RefTestExpired,
    /// RefTestInvitationSent, or RefTestResultSent (excludes RefTestTimeExtended, which has its own public subscription).
    /// </summary>
    /// <param name="id">The ID of the RefTest to monitor</param>
    /// <param name="message">The event message</param>
    /// <returns></returns>
    [Subscribe]
    [Authorize]
    [Topic("{id}")]
    public static IRefTestEvent RefTestUpdated(
        [ID<RefTestDto>] Guid id,
        [EventMessage] object message)
    {
        return message switch
        {
            RefTestStartedEvent e => new RefTestStarted(e.Id, e.Status, e.StartedAt),
            RefTestCompletedEvent e => new RefTestCompleted(e.Id, e.Status, e.CompletedAt, e.QuestionScore,
                e.QuestionTotal, e.AnswerScore, e.AnswerTotal, e.Percentage, e.Language),
            RefTestExpiredEvent e => new RefTestExpired(e.Id, e.Status, e.ExpiredAt),
            RefTestInvitationSentEvent e => new RefTestInvitationSent(e.Id, e.SentAt),
            RefTestResultSentEvent e => new RefTestResultSent(e.Id, e.SentAt),
            RefTestDeletedEvent e => new RefTestDeleted(e.Id, e.Status),
            RefTestResetEvent e => new RefTestReset(e.Id, e.OldStatus),
            RefTestRevivedEvent e => new RefTestRevived(e.Id),
            _ => throw new InvalidOperationException($"Unknown event type: {message.GetType().Name}")
        };
    }

    /// <summary>
    /// Subscribe to ALL RefTest events for ALL RefTests (optimized for list views).
    /// Use this when displaying lists to avoid N subscriptions for N items.
    /// Returns a union type that can be RefTestStarted, RefTestCompleted, RefTestExpired,
    /// RefTestInvitationSent, or RefTestResultSent.
    /// </summary>
    /// <param name="message">The event message containing the RefTest ID and event data</param>
    /// <returns></returns>
    [Subscribe]
    [Authorize]
    [Topic(RefTestSubscriptionService.GlobalTopic)]
    public static IRefTestEvent RefTestsUpdated([EventMessage] object message)
    {
        return message switch
        {
            RefTestStartedEvent e => new RefTestStarted(e.Id, e.Status, e.StartedAt),
            RefTestCompletedEvent e => new RefTestCompleted(e.Id, e.Status, e.CompletedAt, e.QuestionScore,
                e.QuestionTotal, e.AnswerScore, e.AnswerTotal, e.Percentage, e.Language),
            RefTestExpiredEvent e => new RefTestExpired(e.Id, e.Status, e.ExpiredAt),
            RefTestInvitationSentEvent e => new RefTestInvitationSent(e.Id, e.SentAt),
            RefTestResultSentEvent e => new RefTestResultSent(e.Id, e.SentAt),
            RefTestDeletedEvent e => new RefTestDeleted(e.Id, e.Status),
            RefTestResetEvent e => new RefTestReset(e.Id, e.OldStatus),
            RefTestRevivedEvent e => new RefTestRevived(e.Id),
            _ => throw new InvalidOperationException($"Unknown event type: {message.GetType().Name}")
        };
    }

    /// <summary>
    /// Subscribe to acquire a session lock for a specific RefTest token.
    /// PUBLIC: No authorization is required — test takers are not authenticated.
    /// Yields Acquired if no other tab holds the session (or this tab is refreshing),
    /// then keeps the connection open until the client disconnects.
    /// Yields Blocked (and completes) if another tab already holds the session.
    /// Releasing the session happens automatically when the SSE connection drops.
    /// </summary>
    [Subscribe(With = nameof(SubscribeToRefTestSessionLock))]
    public static RefTestSessionEvent RefTestSessionLock([EventMessage] RefTestSessionEvent message) => message;

    public static async IAsyncEnumerable<RefTestSessionEvent> SubscribeToRefTestSessionLock(
        string token,
        string sessionId,
        [Service] IRefTestSessionService sessionService,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!sessionService.TryAcquireSession(token, sessionId))
        {
            // Grace period: on browser refresh the old SSE connection drops within ~100ms.
            // Waiting here allows the existing session to release before we give up.
            // A genuine second tab will still hold its connection throughout the wait → BLOCKED.
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

            if (!sessionService.TryAcquireSession(token, sessionId))
            {
                yield return new RefTestSessionEvent(RefTestSessionStatus.Blocked);
                yield break;
            }
        }

        try
        {
            yield return new RefTestSessionEvent(RefTestSessionStatus.Acquired);
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            sessionService.ReleaseSession(token, sessionId);
        }
    }
}