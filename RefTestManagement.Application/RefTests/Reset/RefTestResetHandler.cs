using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Reset;

public sealed class RefTestResetHandler(
    IRefTestRepository refTestRepository,
    IUnitOfWork unitOfWork,
    IJobEnqueueService jobEnqueueService,
    IRefTestSubscriptionService subscriptionService,
    ICurrentUser currentUser,
    ILogger<RefTestResetHandler> logger)
{
    public async Task<ResetRefTestsResult> ResetAsync(
        IReadOnlyList<Guid> ids,
        RefTestResetType resetType,
        bool regenerateToken,
        CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetByIdsAsync(ids, cancellationToken);
        var resetRefTests = new List<RefTest>();
        var errors = new List<ResetRefTestsError>();
        var resetEvents = new List<(Guid Id, RefTestStatus OldStatus, RefTestStatus Status, DateTime CreatedAt, bool InvitationSent)>();

        foreach (var id in ids)
        {
            RefTest? refTest = null;
            try
            {
                refTest = refTests.FirstOrDefault(candidate => candidate.Id == id);
                if (refTest is null)
                    throw new RefTestNotFoundException(id);

                // Determine if the token will be regenerated
                var willRegenerateToken = resetType == RefTestResetType.Hard
                                         || resetType == RefTestResetType.Soft && regenerateToken;

                // Always cancel result email jobs (results are being cleared in both soft and hard reset)
                // For invitation and expiration jobs, only cancel if the token will be regenerated
                if (willRegenerateToken)
                {
                    // Cancel all pending jobs (invitations with old token, results, expiration checks)
                    await jobEnqueueService.CancelPendingJobsForRefTestAsync(
                        id, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
                }
                else
                {
                    // Soft reset without token regeneration: only cancel result emails
                    // Keep pending invitation emails (token is still valid) and expiration jobs
                    await jobEnqueueService.CancelPendingResultEmailsAsync(
                        id, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
                }

                var invitationWasSent = refTest.InvitationSentAt.HasValue;
                var oldStatus = refTest.Status;
                if (resetType == RefTestResetType.Soft)
                    refTest.SoftReset(regenerateToken);
                else
                    refTest.HardReset();

                // If an invitation was previously sent and the token was regenerated, send a new invitation
                var shouldSendInvitation = invitationWasSent
                                           && (resetType == RefTestResetType.Hard
                                               || resetType == RefTestResetType.Soft && regenerateToken);
                if (shouldSendInvitation)
                {
                    await jobEnqueueService.EnqueueInvitationEmailAsync(
                        refTest, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
                }

                await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
                resetRefTests.Add(refTest);
                resetEvents.Add((refTest.Id, oldStatus, refTest.Status, refTest.CreatedAt, refTest.InvitationSentAt.HasValue));
            }
            catch (Exception exception)
            {
                if (refTest is not null)
                {
                    var restoredRefTest = await unitOfWork.RestoreChangesAsync(refTest, cancellationToken);
                    var index = refTests.IndexOf(refTest);
                    if (index >= 0)
                    {
                        if (restoredRefTest is null)
                            refTests.RemoveAt(index);
                        else
                            refTests[index] = restoredRefTest;
                    }
                }

                errors.Add(new ResetRefTestsError(id, MutationFailureHandling.GetUserSafeMessage(exception)));
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, "ResetRefTestsAsync", currentUser.CorrelationId, id);
            }
        }

        foreach (var resetEvent in resetEvents)
            await subscriptionService.PublishRefTestResetAsync(
                resetEvent.Id, resetEvent.OldStatus, resetType, resetEvent.Status, resetEvent.CreatedAt,
                resetEvent.InvitationSent, cancellationToken);

        return new ResetRefTestsResult(ids.Count, resetRefTests, errors, resetEvents.Count);
    }

    public async Task<ReviveRefTestsResult> ReviveAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetByIdsAsync(ids, cancellationToken);
        var revivedRefTests = new List<RefTest>();
        var errors = new List<ReviveRefTestsError>();
        var revivedEvents = new List<(Guid Id, RefTestStatus Status, DateTime CreatedAt, bool InvitationSent)>();
        foreach (var id in ids)
        {
            var refTest = refTests.FirstOrDefault(candidate => candidate.Id == id);
            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id);

                // Cancel any pending jobs for this RefTest to prevent outdated operations
                // (invitations with old token, results with old scores, expiration checks)
                await jobEnqueueService.CancelPendingJobsForRefTestAsync(
                    id, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);

                var invitationWasSent = refTest.InvitationSentAt.HasValue;
                refTest.Revive();

                // If an invitation was previously sent, send a new one with the new token
                // (Revive always regenerates the token)
                if (invitationWasSent)
                {
                    await jobEnqueueService.EnqueueInvitationEmailAsync(
                        refTest, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
                }

                await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
                revivedRefTests.Add(refTest);
                revivedEvents.Add((refTest.Id, refTest.Status, refTest.CreatedAt, refTest.InvitationSentAt.HasValue));
            }
            catch (Exception exception)
            {
                if (refTest is not null)
                {
                    var restoredRefTest = await unitOfWork.RestoreChangesAsync(refTest, cancellationToken);
                    var index = refTests.IndexOf(refTest);
                    if (index >= 0)
                    {
                        if (restoredRefTest is null)
                            refTests.RemoveAt(index);
                        else
                            refTests[index] = restoredRefTest;
                    }
                }

                errors.Add(new ReviveRefTestsError(id, MutationFailureHandling.GetUserSafeMessage(exception)));
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, "ReviveRefTestsAsync", currentUser.CorrelationId, id);
            }
        }

        foreach (var revivedEvent in revivedEvents)
            await subscriptionService.PublishRefTestRevivedAsync(
                revivedEvent.Id, revivedEvent.Status, revivedEvent.CreatedAt, revivedEvent.InvitationSent, cancellationToken);
        return new ReviveRefTestsResult(ids.Count, revivedRefTests, errors, revivedRefTests.Count);
    }
}
