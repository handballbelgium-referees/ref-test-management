using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
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
    [Authorize]
    public static async Task<ResetRefTestsResult> ResetRefTestsAsync(
        ResetRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var result = new ResetRefTestsResult
        {
            TotalRequested = input.RefTestIds.Count
        };

        var refTests = await context.RefTests
            .Where(rt => input.RefTestIds.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<ResetRefTestsError>();

        foreach (var id in input.RefTestIds)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id);

                if (refTest == null)
                    throw new RefTestNotFoundException(id);

                // Check if the invitation was previously sent
                var invitationWasSent = refTest.InvitationSentAt.HasValue;

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

        await context.SaveChangesAsync(cancellationToken);

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
    /// <param name="refTestIds"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static async Task<ReviveRefTestsResult> ReviveExpiredRefTestsAsync(
        List<Guid> refTestIds,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var result = new ReviveRefTestsResult
        {
            TotalRequested = refTestIds.Count
        };

        var refTests = await context.RefTests
            .Where(rt => refTestIds.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<ReviveRefTestsError>();

        foreach (var id in refTestIds)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id);

                if (refTest == null)
                    throw new RefTestNotFoundException(id);

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

        await context.SaveChangesAsync(cancellationToken);

        return result with
        {
            SuccessfullyRevived = successCount,
            Failed = failedCount,
            Errors = errors
        };
    }
}
