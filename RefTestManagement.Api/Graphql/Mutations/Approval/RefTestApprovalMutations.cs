using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Permissions;
using Handball.Belgium.RefTestManagement.AuditLog;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;

[MutationType]
public static class RefTestApprovalMutations
{
    [Authorize(Policy = Permission.RefTests.Approve)]
    [AuditAction(AuditLogAction.RefTest.Approve)]
    public static async Task<ApproveRefTestsResult> ApproveRefTestsAsync(
        ApproveRefTestsInput input,
        RefTestManagementContext context,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTests = await context.RefTests
            .Where(rt => input.Ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var userEmail = httpContextAccessor.HttpContext?.User.GetEmail() ?? "unknown";
        var result = new ApproveRefTestsResult { TotalRequested = input.Ids.Count };

        foreach (var id in input.Ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id)
                    ?? throw new RefTestNotFoundException(id);

                refTest.Approve(userEmail);
                result.ApprovedRefTests.Add(refTest.ToDto());
                result.SuccessfullyApproved++;
            }
            catch (Exception e)
            {
                result.Failed++;
                result.Errors.Add(new ApprovalError { RefTestId = id, ErrorMessage = e.Message });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var refTest in refTests.Where(rt => rt.ApprovalStatus == Domain.RefTests.ApprovalStatus.Approved
                                                     && rt.SendInvitationsAutomatically))
        {
            var payload = new InvitationEmailPayload(
                refTest.Id,
                refTest.FullName,
                refTest.Email,
                refTest.Token,
                refTest.NumberOfQuestions,
                refTest.MaxTimeInMinutes);

            await jobEnqueueService.EnqueueInvitationEmailAsync(payload, cancellationToken: cancellationToken);
        }

        return result;
    }

    [Authorize(Policy = Permission.RefTests.Approve)]
    [AuditAction(AuditLogAction.RefTest.Reject)]
    public static async Task<RejectRefTestsResult> RejectRefTestsAsync(
        RejectRefTestsInput input,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var refTests = await context.RefTests
            .Where(rt => input.Ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var result = new RejectRefTestsResult { TotalRequested = input.Ids.Count };

        foreach (var id in input.Ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id)
                    ?? throw new RefTestNotFoundException(id);

                refTest.Reject(input.Reason);
                result.RejectedRefTests.Add(refTest.ToDto());
                result.SuccessfullyRejected++;
            }
            catch (Exception e)
            {
                result.Failed++;
                result.Errors.Add(new ApprovalError { RefTestId = id, ErrorMessage = e.Message });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return result;
    }
}
