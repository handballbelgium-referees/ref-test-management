using System.Runtime.CompilerServices;
using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using MicrosoftAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

/// <summary>
/// RefTest subscriptions for real-time updates
/// </summary>
[SubscriptionType]
public static partial class RefTestSubscriptions
{
    private static readonly TimeSpan SessionLockRevalidationInterval = TimeSpan.FromSeconds(30);

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
    [Subscribe]
    [Authorize(Policy = Permissions.RefTests.ViewDetail)]
    [Topic("{id}")]
    public static async Task<IRefTestEvent> RefTestUpdated(
        [ID<RefTestDto>] Guid id,
        [EventMessage] object message,
        ClaimsPrincipal user,
        [Service] IPermissionSnapshotService permissionSnapshotService,
        [Service] MicrosoftAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        await ValidateCurrentPermissionAsync(
            user,
            Permissions.RefTests.ViewDetail,
            permissionSnapshotService,
            authorizationService,
            cancellationToken);

        return message switch
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
    [Subscribe]
    [Authorize(Policy = Permissions.RefTests.ViewList)]
    [Topic(RefTestSubscriptionService.GlobalTopic)]
    public static async Task<IRefTestEvent> RefTestsUpdated(
        [EventMessage] object message,
        ClaimsPrincipal user,
        [Service] IPermissionSnapshotService permissionSnapshotService,
        [Service] MicrosoftAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        await ValidateCurrentPermissionAsync(
            user,
            Permissions.RefTests.ViewList,
            permissionSnapshotService,
            authorizationService,
            cancellationToken);

        return message switch
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

    private static async Task ValidateCurrentPermissionAsync(
        ClaimsPrincipal user,
        string requiredPermission,
        IPermissionSnapshotService permissionSnapshotService,
        MicrosoftAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        // Handshake authorization alone is not enough: a WebSocket can outlive the original principal.
        var permissions = await permissionSnapshotService.GetCurrentPermissionsAsync(user, cancellationToken);
        if (permissions is null)
            throw new GraphQLException("The subscription permission could not be verified.");

        var freshPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            permissions.Select(permission => new Claim("permissions", permission)),
            "PermissionSnapshot"));
        var result = await authorizationService.AuthorizeAsync(
            freshPrincipal,
            resource: null,
            policyName: requiredPermission);
        if (!result.Succeeded)
            throw new GraphQLException("The subscription permission is no longer available.");
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