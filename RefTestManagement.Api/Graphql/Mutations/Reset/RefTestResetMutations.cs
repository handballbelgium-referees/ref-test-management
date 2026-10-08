using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

/// <summary>
/// RefTest reset mutations
/// </summary>
[MutationType]
public static partial class RefTestResetMutations
{
    /// <summary>
    /// Reset one or more RefTests to allow retake. Soft reset preserves the audit trail, hard reset clears everything.
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to reset and the reset type.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing and canceling job notifications.</param>
    /// <param name="subscriptionService">Service for managing RefTest subscriptions.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for accessing the current HTTP context.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Reset)]
    public static async Task<ResetRefTestsResult> ResetRefTestsAsync(
        ResetRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestResetMutations));
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);
        var result = new ResetRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            ResetRefTests = [],
            Errors = []
        };

        var refTests = await context.RefTests
            .Where(rt => input.Ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var errors = new List<ResetRefTestsError>();
        var resetEvents = new List<(
            Guid Id,
            RefTestStatus OldStatus,
            RefTestStatus Status,
            DateTime CreatedAt,
            bool InvitationSent)>();

        foreach (var id in input.Ids)
        {
            RefTest? refTest = null;
            try
            {
                refTest = refTests.FirstOrDefault(rt => rt.Id == id);

                if (refTest == null)
                    throw new RefTestNotFoundException(id);

                // Determine if the token will be regenerated
                var willRegenerateToken = input.ResetType == RefTestResetType.Hard ||
                                         input is { ResetType: RefTestResetType.Soft, RegenerateToken: true };

                // Always cancel result email jobs (results are being cleared in both soft and hard reset)
                // For invitation and expiration jobs, only cancel if the token will be regenerated
                if (willRegenerateToken)
                {
                    // Cancel all pending jobs (invitations with old token, results, expiration checks)
                    await jobEnqueueService.CancelPendingJobsForRefTestAsync(id,
                        saveChanges: false, unitOfWorkContext: context, cancellationToken: cancellationToken);
                }
                else
                {
                    // Soft reset without token regeneration: only cancel result emails
                    // Keep pending invitation emails (token is still valid) and expiration jobs
                    await jobEnqueueService.CancelPendingResultEmailsAsync(id,
                        saveChanges: false, unitOfWorkContext: context, cancellationToken: cancellationToken);
                }

                // Check if the invitation was previously sent
                var invitationWasSent = refTest.InvitationSentAt.HasValue;

                var oldStatus = refTest.Status;

                if (input.ResetType == RefTestResetType.Soft)
                {
                    refTest.SoftReset(input.RegenerateToken);
                }
                else
                {
                    refTest.HardReset();
                }

                // If an invitation was previously sent and the token was regenerated, send a new invitation
                var shouldSendInvitation = invitationWasSent &&
                                           (input.ResetType == RefTestResetType.Hard ||
                                            input is { ResetType: RefTestResetType.Soft, RegenerateToken: true });

                if (shouldSendInvitation)
                {
                    await jobEnqueueService.EnqueueInvitationEmailAsync(refTest,
                        saveChanges: false,
                        unitOfWorkContext: context,
                        cancellationToken: cancellationToken);
                }

                var refTestDto = refTest.ToDto();
                await context.SaveChangesWithRetryAsync(cancellationToken);

                result.ResetRefTests.Add(refTestDto);
                resetEvents.Add((
                    refTest.Id,
                    oldStatus,
                    refTest.Status,
                    refTest.CreatedAt,
                    refTest.InvitationSentAt.HasValue));
            }
            catch (Exception ex)
            {
                if (refTest is not null)
                {
                    var restoredRefTest = await RollbackFailedRefTestOperationAsync(
                        context, refTest, cancellationToken);
                    var refTestIndex = refTests.IndexOf(refTest);
                    if (refTestIndex >= 0)
                    {
                        if (restoredRefTest is null)
                            refTests.RemoveAt(refTestIndex);
                        else
                            refTests[refTestIndex] = restoredRefTest;
                    }
                }

                errors.Add(new ResetRefTestsError
                {
                    RefTestId = id,
                    ErrorMessage = MutationErrorHandling.GetUserSafeMessage(ex)
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(ResetRefTestsAsync), correlationId, id);
            }
        }

        foreach (var resetEvent in resetEvents)
        {
            await subscriptionService.PublishRefTestResetAsync(
                resetEvent.Id,
                resetEvent.OldStatus,
                input.ResetType,
                resetEvent.Status,
                resetEvent.CreatedAt,
                resetEvent.InvitationSent,
                cancellationToken);
        }

        return result with
        {
            SuccessfullyReset = resetEvents.Count,
            Failed = input.Ids.Count - resetEvents.Count,
            Errors = errors
        };
    }

    /// <summary>
    /// Revive one or more expired RefTests by resetting them to Pending status with a new token and fresh expiration timer
    /// </summary>
    /// <param name="ids">The IDs of the RefTests to revive.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing and canceling job notifications.</param>
    /// <param name="subscriptionService">Service for managing RefTest subscriptions.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for accessing the current HTTP context.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Revive)]
    public static async Task<ReviveRefTestsResult> ReviveRefTestsAsync(
        [ID<RefTestDto>] List<Guid> ids,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestResetMutations));
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);
        var result = new ReviveRefTestsResult
        {
            TotalRequested = ids.Count,
            RevivedRefTests = [],
            Errors = []
        };

        var refTests = await context.RefTests
            .Where(rt => ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<ReviveRefTestsError>();
        var revivedEvents = new List<(Guid Id, RefTestStatus Status, DateTime CreatedAt, bool InvitationSent)>();

        foreach (var id in ids)
        {
            var refTest = refTests.FirstOrDefault(rt => rt.Id == id);
            try
            {
                if (refTest == null)
                    throw new RefTestNotFoundException(id);

                // Cancel any pending jobs for this RefTest to prevent outdated operations
                // (invitations with old token, results with old scores, expiration checks)
                await jobEnqueueService.CancelPendingJobsForRefTestAsync(id,
                    saveChanges: false, unitOfWorkContext: context, cancellationToken: cancellationToken);

                // Check if the invitation was previously sent
                var invitationWasSent = refTest.InvitationSentAt.HasValue;

                refTest.Revive();

                // If an invitation was previously sent, send a new one with the new token
                // (Revive always regenerates the token)
                if (invitationWasSent)
                {
                    await jobEnqueueService.EnqueueInvitationEmailAsync(refTest,
                        saveChanges: false,
                        unitOfWorkContext: context,
                        cancellationToken: cancellationToken);
                }

                var refTestDto = refTest.ToDto();
                await context.SaveChangesWithRetryAsync(cancellationToken);

                successCount++;
                result.RevivedRefTests.Add(refTestDto);
                revivedEvents.Add((
                    refTest.Id,
                    refTest.Status,
                    refTest.CreatedAt,
                    refTest.InvitationSentAt.HasValue));
            }
            catch (Exception ex)
            {
                if (refTest is not null)
                {
                    var restoredRefTest = await RollbackFailedRefTestOperationAsync(
                        context, refTest, cancellationToken);
                    var refTestIndex = refTests.IndexOf(refTest);
                    if (refTestIndex >= 0)
                    {
                        if (restoredRefTest is null)
                            refTests.RemoveAt(refTestIndex);
                        else
                            refTests[refTestIndex] = restoredRefTest;
                    }
                }

                failedCount++;
                errors.Add(new ReviveRefTestsError
                {
                    RefTestId = id,
                    ErrorMessage = MutationErrorHandling.GetUserSafeMessage(ex)
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(ReviveRefTestsAsync), correlationId, id);
            }
        }

        foreach (var revivedEvent in revivedEvents)
        {
            await subscriptionService.PublishRefTestRevivedAsync(
                revivedEvent.Id,
                revivedEvent.Status,
                revivedEvent.CreatedAt,
                revivedEvent.InvitationSent,
                cancellationToken);
        }

        return result with
        {
            SuccessfullyRevived = successCount,
            Failed = failedCount,
            Errors = errors
        };
    }

    /// <summary>Discards staged job/entity changes and returns a clean copy of the persisted RefTest.</summary>
    /// <param name="context">The RefTest unit of work.</param>
    /// <param name="refTest">The aggregate modified by the failed reset or revive attempt.</param>
    /// <param name="cancellationToken">Token for the cleanup queries.</param>
    private static async Task<RefTest?> RollbackFailedRefTestOperationAsync(
        RefTestManagementContext context,
        RefTest refTest,
        CancellationToken cancellationToken)
    {
        var refTestId = refTest.Id;
        var changedEntries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        try
        {
            foreach (var entry in changedEntries)
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
                else
                    await entry.ReloadAsync(cancellationToken);
            }
        }
        finally
        {
            // Domain events are transient and are not restored by EF's ReloadAsync.
            refTest.ClearDomainEvents();
        }

        // Reload does not restore RefTest's transient IssuedToken. Detach it and query a clean
        // aggregate so a failed token rotation cannot leak into a later item in this batch.
        context.Entry(refTest).State = EntityState.Detached;
        return await context.RefTests.FirstOrDefaultAsync(candidate => candidate.Id == refTestId, cancellationToken);
    }
}
