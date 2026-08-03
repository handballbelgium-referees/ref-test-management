using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
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
public static class RefTestResetMutations
{
    /// <summary>
    /// Reset one or more RefTests to allow retake. Soft reset preserves the audit trail, hard reset clears everything.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Reset)]
    public static async Task<ResetRefTestsResult> ResetRefTestsAsync(
        ResetRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        var result = new ResetRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            ResetRefTests = [],
            Errors = []
        };

        var refTests = await context.RefTests
            .Where(rt => input.Ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<ResetRefTestsError>();
        var resetEvents = new List<(Guid Id, RefTestStatus OldStatus)>();

        foreach (var id in input.Ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id);

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
                    await jobEnqueueService.CancelPendingJobsForRefTestAsync(id, cancellationToken);
                }
                else
                {
                    // Soft reset without token regeneration: only cancel result emails
                    // Keep pending invitation emails (token is still valid) and expiration jobs
                    await jobEnqueueService.CancelPendingResultEmailsAsync(id, cancellationToken);
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
                    var invitationPayload = new InvitationEmailPayload(
                        refTest.Id,
                        refTest.FullName,
                        refTest.Email,
                        refTest.Token,
                        refTest.NumberOfQuestions,
                        refTest.MaxTimeInMinutes
                    );

                    await jobEnqueueService.EnqueueInvitationEmailAsync(invitationPayload,
                        cancellationToken: cancellationToken);
                }

                successCount++;
                result.ResetRefTests.Add(refTest.ToDto());
                resetEvents.Add((refTest.Id, oldStatus));
                
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add(new ResetRefTestsError
                {
                    RefTestId = id,
                    ErrorMessage = ex.Message
                });
            }
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        foreach (var (refTestId, oldStatus) in resetEvents)
        {
            await subscriptionService.PublishRefTestResetAsync(refTestId, oldStatus, cancellationToken);
        }

        return result with
        {
            SuccessfullyReset = successCount,
            Failed = failedCount,
            Errors = errors
        };
    }

    /// <summary>
    /// Revive one or more expired RefTests by resetting them to Pending status with a new token and fresh expiration timer
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Revive)]
    public static async Task<ReviveRefTestsResult> ReviveRefTestsAsync(
        [ID<RefTestDto>] List<Guid> ids,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
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
        var revivedIds = new List<Guid>();

        foreach (var id in ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id);

                if (refTest == null)
                    throw new RefTestNotFoundException(id);

                // Cancel any pending jobs for this RefTest to prevent outdated operations
                // (invitations with old token, results with old scores, expiration checks)
                await jobEnqueueService.CancelPendingJobsForRefTestAsync(id, cancellationToken);

                // Check if the invitation was previously sent
                var invitationWasSent = refTest.InvitationSentAt.HasValue;

                refTest.Revive();

                // If an invitation was previously sent, send a new one with the new token
                // (Revive always regenerates the token)
                if (invitationWasSent)
                {
                    var invitationPayload = new InvitationEmailPayload(
                        refTest.Id,
                        refTest.FullName,
                        refTest.Email,
                        refTest.Token,
                        refTest.NumberOfQuestions,
                        refTest.MaxTimeInMinutes
                    );

                    await jobEnqueueService.EnqueueInvitationEmailAsync(invitationPayload,
                        cancellationToken: cancellationToken);
                }

                successCount++;
                result.RevivedRefTests.Add(refTest.ToDto());
                revivedIds.Add(refTest.Id);
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add(new ReviveRefTestsError
                {
                    RefTestId = id,
                    ErrorMessage = ex.Message
                });
            }
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        foreach (var refTestId in revivedIds)
        {
            await subscriptionService.PublishRefTestRevivedAsync(refTestId, cancellationToken);
        }

        return result with
        {
            SuccessfullyRevived = successCount,
            Failed = failedCount,
            Errors = errors
        };
    }
}
