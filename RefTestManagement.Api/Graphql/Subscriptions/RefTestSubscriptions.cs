using System.Runtime.CompilerServices;
using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

/// <summary>
/// RefTest subscriptions for real-time updates
/// </summary>
[SubscriptionType]
public static partial class RefTestSubscriptions
{
    private static readonly TimeSpan SessionLockRevalidationInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SubscriptionPermissionRecheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SubscriptionEventRecheckThrottle = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Subscribe to time extension events for a specific RefTest.
    /// PUBLIC: No authorization required - test takers need to see time extensions in real-time.
    /// </summary>
    /// <param name="id">The ID of the RefTest to monitor</param>
    /// <param name="message">The time extension event message</param>
    /// <returns>The RefTestTimeExtended event</returns>
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
    /// <returns>The RefTest event</returns>
    [Subscribe(With = nameof(SubscribeToRefTestUpdated))]
    [Authorize(Policy = Permissions.RefTests.ViewDetail)]
    public static IRefTestEvent RefTestUpdated(
        [EventMessage] RefTestSubscriptionMessage message)
    {
        return message.DeserializeEvent() switch
        {
            RefTestStartedEvent e => new RefTestStarted(e.Id, e.Status, e.StartedAt),
            RefTestCompletedEvent e => new RefTestCompleted(e.Id, e.Status, e.CompletedAt, e.QuestionScore,
                e.QuestionTotal, e.AnswerScore, e.AnswerTotal, e.Percentage, e.Language, e.SelectedAnswerIds),
            RefTestExpiredEvent e => new RefTestExpired(e.Id, e.Status, e.ExpiredAt),
            RefTestInvitationSentEvent e => new RefTestInvitationSent(e.Id, e.SentAt),
            RefTestResultSentEvent e => new RefTestResultSent(e.Id, e.SentAt),
            RefTestDeletedEvent e => new RefTestDeleted(e.Id, e.Status),
            RefTestAnonymizedEvent e => new RefTestAnonymized(e.Id, e.Status, e.FullName, e.Email),
            RefTestResetEvent e => new RefTestReset(
                e.Id, e.OldStatus, e.Status, e.ResetType, e.CreatedAt, e.InvitationSent),
            RefTestRevivedEvent e => new RefTestRevived(
                e.Id, e.Status, e.CreatedAt, e.InvitationSent),
            RefTestCreatedEvent e => new RefTestCreated(
                e.Id, e.FullName, e.Email, e.TitleId, e.TitleValue, e.InvitationSent, e.ResultsSent,
                e.SendInvitationsAutomatically, e.SendResultsAutomatically, e.Status, e.NumberOfQuestions,
                e.MaxTimeInMinutes, e.FirstName, e.LastName, e.CreatedAt, e.ScheduledAt),
            RefTestApprovedEvent e => new RefTestApproved(
                e.Id, e.OldStatus, e.Status, e.ApprovedAt, e.CreatedAt),
            RefTestRejectedEvent e => new RefTestRejected(e.Id, e.Status, e.Reason, e.RejectedAt),
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
    /// <returns>The RefTest event</returns>
    [Subscribe(With = nameof(SubscribeToRefTestsUpdated))]
    [Authorize(Policy = Permissions.RefTests.ViewList)]
    public static IRefTestEvent RefTestsUpdated(
        [EventMessage] RefTestSubscriptionMessage message)
    {
        return message.DeserializeEvent() switch
        {
            RefTestStartedEvent e => new RefTestStarted(e.Id, e.Status, e.StartedAt),
            RefTestCompletedEvent e => new RefTestCompleted(e.Id, e.Status, e.CompletedAt, e.QuestionScore,
                e.QuestionTotal, e.AnswerScore, e.AnswerTotal, e.Percentage, e.Language, null),
            RefTestExpiredEvent e => new RefTestExpired(e.Id, e.Status, e.ExpiredAt),
            RefTestInvitationSentEvent e => new RefTestInvitationSent(e.Id, e.SentAt),
            RefTestResultSentEvent e => new RefTestResultSent(e.Id, e.SentAt),
            RefTestDeletedEvent e => new RefTestDeleted(e.Id, e.Status),
            RefTestAnonymizedEvent e => new RefTestAnonymized(e.Id, e.Status, e.FullName, e.Email),
            RefTestResetEvent e => new RefTestReset(
                e.Id, e.OldStatus, e.Status, e.ResetType, e.CreatedAt, e.InvitationSent),
            RefTestRevivedEvent e => new RefTestRevived(
                e.Id, e.Status, e.CreatedAt, e.InvitationSent),
            RefTestCreatedEvent e => new RefTestCreated(
                e.Id, e.FullName, e.Email, e.TitleId, e.TitleValue, e.InvitationSent, e.ResultsSent,
                e.SendInvitationsAutomatically, e.SendResultsAutomatically, e.Status, e.NumberOfQuestions,
                e.MaxTimeInMinutes, e.FirstName, e.LastName, e.CreatedAt, e.ScheduledAt),
            RefTestApprovedEvent e => new RefTestApproved(
                e.Id, e.OldStatus, e.Status, e.ApprovedAt, e.CreatedAt),
            RefTestRejectedEvent e => new RefTestRejected(e.Id, e.Status, e.Reason, e.RejectedAt),
            _ => throw new InvalidOperationException($"Unknown event type: {message.GetType().Name}")
        };
    }

    /// <summary>
    /// Source stream for <see cref="RefTestUpdated"/>: one RefTest topic, re-checked live.
    /// </summary>
    public static IAsyncEnumerable<RefTestSubscriptionMessage> SubscribeToRefTestUpdated(
        [ID<RefTestDto>] Guid id,
        ClaimsPrincipal user,
        [Service] ITopicEventReceiver topicEventReceiver,
        [Service] ILivePermissionVerifier permissionVerifier,
        [Service] TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        StreamWhilePermitted(
            id.ToString(),
            Permissions.RefTests.ViewDetail,
            user,
            topicEventReceiver,
            permissionVerifier,
            timeProvider,
            SubscriptionPermissionRecheckInterval,
            SubscriptionEventRecheckThrottle,
            cancellationToken);

    /// <summary>
    /// Source stream for <see cref="RefTestsUpdated"/>: the global RefTest topic, re-checked live.
    /// </summary>
    public static IAsyncEnumerable<RefTestSubscriptionMessage> SubscribeToRefTestsUpdated(
        ClaimsPrincipal user,
        [Service] ITopicEventReceiver topicEventReceiver,
        [Service] ILivePermissionVerifier permissionVerifier,
        [Service] TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        StreamWhilePermitted(
            RefTestSubscriptionService.GlobalTopic,
            Permissions.RefTests.ViewList,
            user,
            topicEventReceiver,
            permissionVerifier,
            timeProvider,
            SubscriptionPermissionRecheckInterval,
            SubscriptionEventRecheckThrottle,
            cancellationToken);

    /// <summary>
    /// Forwards topic events while the subscriber still holds <paramref name="permission"/>.
    /// The stream ends when the permission is revoked, on a timer tick (even when idle) or
    /// before an event whose last live check is older than the throttle. A failed or timed-out
    /// live check ends the stream (fail closed).
    /// </summary>
    internal static async IAsyncEnumerable<RefTestSubscriptionMessage> StreamWhilePermitted(
        string topic,
        string permission,
        ClaimsPrincipal user,
        ITopicEventReceiver topicEventReceiver,
        ILivePermissionVerifier permissionVerifier,
        TimeProvider timeProvider,
        TimeSpan recheckInterval,
        TimeSpan eventRecheckThrottle,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linkedSource.Token;

        await using var source = await topicEventReceiver.SubscribeAsync<RefTestSubscriptionMessage>(topic, token);
        var events = source.ReadEventsAsync().GetAsyncEnumerator(token);
        using var timer = new PeriodicTimer(recheckInterval, timeProvider);

        if (!await permissionVerifier.HasPermissionAsync(user, permission, token))
            yield break;

        var lastCheckedAt = timeProvider.GetUtcNow();
        Task<bool>? nextEvent = null;
        Task<bool>? nextTick = null;
        try
        {
            nextEvent = events.MoveNextAsync().AsTask();
            nextTick = timer.WaitForNextTickAsync(token).AsTask();

            while (true)
            {
                var completed = await Task.WhenAny(nextEvent, nextTick);
                if (completed == nextTick)
                {
                    if (!await nextTick)
                        yield break;

                    nextTick = timer.WaitForNextTickAsync(token).AsTask();
                    if (!await permissionVerifier.HasPermissionAsync(user, permission, token))
                        yield break;

                    lastCheckedAt = timeProvider.GetUtcNow();
                    continue;
                }

                if (!await nextEvent)
                    yield break;

                var message = events.Current;
                nextEvent = events.MoveNextAsync().AsTask();

                var now = timeProvider.GetUtcNow();
                if (now - lastCheckedAt >= eventRecheckThrottle)
                {
                    if (!await permissionVerifier.HasPermissionAsync(user, permission, token))
                        yield break;

                    lastCheckedAt = now;
                }

                yield return message;
            }
        }
        finally
        {
            // Dispose the source first so pending reads complete even when it ignores cancellation.
            linkedSource.Cancel();
            await source.DisposeAsync();
            await AwaitCancelledAsync(nextEvent);
            await AwaitCancelledAsync(nextTick);
            await events.DisposeAsync();
        }
    }

    private static async Task AwaitCancelledAsync(Task<bool>? pending)
    {
        if (pending is null)
            return;

        try
        {
            await pending;
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Subscribe to acquire a session lock for a participant credential.
    /// PUBLIC: No authorization is required — test takers are not authenticated.
    /// Yields Acquired if no other tab holds the session (or this tab is refreshing),
    /// then revalidates the credential periodically until the client disconnects or the
    /// participant's access is revoked.
    /// Yields Blocked (and completes) if another tab already holds the session.
    /// Releasing the session happens automatically when the SSE connection drops.
    /// </summary>
    [Subscribe(With = nameof(SubscribeToRefTestSessionLock))]
    public static RefTestSessionEvent RefTestSessionLock([EventMessage] RefTestSessionEvent message) => message;

    public static async IAsyncEnumerable<RefTestSessionEvent> SubscribeToRefTestSessionLock(
        string token,
        string sessionId,
        RefTestManagementContext context,
        [Service] IRefTestSessionService sessionService,
        [Service] IRefTestSessionTokenService sessionTokenService,
        [Service] TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!RefTest.IsValidTokenFormat(token)
            && !RefTestSessionTokenService.HasSessionTokenFormat(token))
        {
            yield return new RefTestSessionEvent(RefTestSessionStatus.Blocked);
            yield break;
        }

        var refTest = await context.RefTests
            .AsNoTracking()
            .FindByParticipantCredentialAsync(token, sessionTokenService, cancellationToken);
        if (refTest is null || refTest.IsAnonymized)
        {
            yield return new RefTestSessionEvent(RefTestSessionStatus.Blocked);
            yield break;
        }

        var lockKey = refTest.Id.ToString("N");
        var lockStatus = refTest.Status;
        if (!await sessionService.TryAcquireSessionAsync(lockKey, sessionId, cancellationToken))
        {
            // Grace period: on browser refresh the old SSE connection drops within ~100ms.
            // Waiting here allows the existing session to release before we give up.
            // A genuine second tab will still hold its connection throughout the wait → BLOCKED.
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

            if (!await sessionService.TryAcquireSessionAsync(lockKey, sessionId, cancellationToken))
            {
                yield return new RefTestSessionEvent(RefTestSessionStatus.Blocked);
                yield break;
            }
        }

        try
        {
            using var timer = new PeriodicTimer(SessionLockRevalidationInterval, timeProvider);
            yield return new RefTestSessionEvent(RefTestSessionStatus.Acquired);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                // A lost lease means another tab took over after this one stopped renewing.
                if (!await sessionService.RenewSessionAsync(lockKey, sessionId, cancellationToken))
                    yield break;

                var currentRefTest = await context.RefTests
                    .AsNoTracking()
                    .FindByParticipantCredentialAsync(token, sessionTokenService, cancellationToken);
                if (currentRefTest is null || currentRefTest.IsAnonymized)
                    yield break;

                if (currentRefTest.Status != lockStatus)
                {
                    if (lockStatus != RefTestStatus.Pending
                        || currentRefTest.Status != RefTestStatus.InProgress)
                    {
                        yield break;
                    }

                    lockStatus = RefTestStatus.InProgress;
                }
            }
        }
        finally
        {
            await sessionService.ReleaseSessionAsync(lockKey, sessionId);
        }
    }
}