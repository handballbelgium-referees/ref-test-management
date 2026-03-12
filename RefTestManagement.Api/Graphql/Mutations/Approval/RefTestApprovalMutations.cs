using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Permissions;
using Handball.Belgium.RefTestManagement.Permissions.AuditLog;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;

[MutationType]
public static class RefTestApprovalMutations
{
    [Authorize(Policy = Permission.RefTests.Approve)]
    [AuditAction(AuditLogAction.RefTest.Approve)]
    [Error<RefTestNotFoundException>]
    public static async Task<RefTestDto> ApproveRefTestAsync(
        [ID<RefTest>] Guid refTestId,
        RefTestManagementContext context,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == refTestId, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(refTestId);

        var userEmail = httpContextAccessor.HttpContext?.User.GetEmail() ?? "unknown";
        refTest.Approve(userEmail);

        await context.SaveChangesAsync(cancellationToken);

        return refTest.ToDto();
    }

    [Authorize(Policy = Permission.RefTests.Approve)]
    [AuditAction(AuditLogAction.RefTest.Reject)]
    [Error<RefTestNotFoundException>]
    public static async Task<RefTestDto> RejectRefTestAsync(
        RejectRefTestInput input,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == input.RefTestId, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(input.RefTestId);

        refTest.Reject(input.Reason);

        await context.SaveChangesAsync(cancellationToken);

        return refTest.ToDto();
    }
}
