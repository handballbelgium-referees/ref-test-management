using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
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
    /// Reset a RefTest to allow retake. Soft reset preserves the audit trail, hard reset clears everything.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> ResetRefTestAsync(
        ResetRefTestInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == input.RefTestId, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(input.RefTestId);

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

        await context.SaveChangesAsync(cancellationToken);

        // If an invitation was previously sent and the token was regenerated, send a new invitation
        var shouldSendInvitation = invitationWasSent &&
                                   (input.ResetType == RefTestResetType.Hard ||
                                    input is { ResetType: RefTestResetType.Soft, RegenerateToken: true });

        if (!shouldSendInvitation)
            return refTest.ToDto();

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

        return refTest.ToDto();
    }

    /// <summary>
    /// Revive an expired RefTest by resetting it to Pending status with a new token and fresh expiration timer
    /// </summary>
    /// <param name="refTestId"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> ReviveExpiredRefTestAsync(
        Guid refTestId,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == refTestId, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(refTestId);

        // Check if the invitation was previously sent
        var invitationWasSent = refTest.InvitationSentAt.HasValue;

        refTest.Revive();
        await context.SaveChangesAsync(cancellationToken);

        if (!invitationWasSent)
            return refTest.ToDto();

        // If an invitation was previously sent, send a new one with the new token
        // (Revive always regenerates the token)
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

        return refTest.ToDto();
    }
}
