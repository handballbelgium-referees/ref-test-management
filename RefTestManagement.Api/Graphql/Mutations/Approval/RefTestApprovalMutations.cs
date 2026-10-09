using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests.Approval;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;

/// <summary>
/// Mutations for approving or rejecting RefTests that are awaiting approval
/// </summary>
[MutationType]
public static partial class RefTestApprovalMutations
{
    /// <summary>
    /// Approve one or more RefTests that are pending approval (or previously rejected).
    /// Approved RefTests transition to Pending status and invitation emails are enqueued
    /// if the RefTest was configured for automated invitations.
    /// </summary>
    /// <param name="input">The input parameters for RefTest approval.</param>
    /// <param name="handler">Application handler for approval operations.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    [Authorize(Policy = Permissions.RefTests.Approve)]
    public static async Task<ApproveRefTestsResult> ApproveRefTestsAsync(
        ApproveRefTestsInput input,
        [Service] RefTestApprovalHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ApproveAsync(input.Ids, cancellationToken);
        return new ApproveRefTestsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullyApproved = result.ApprovedRefTests.Count,
            Failed = result.TotalRequested - result.ApprovedRefTests.Count,
            ApprovedRefTests = result.ApprovedRefTests.Select(refTest => refTest.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new ApproveRefTestsError
            {
                RefTestId = error.RefTestId,
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }

    /// <summary>
    /// Reject one or more RefTests that are pending approval.
    /// A non-empty rejection reason is required.
    /// </summary>
    /// <param name="input">The input parameters for RefTest rejection.</param>
    /// <param name="handler">Application handler for approval operations.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    [Authorize(Policy = Permissions.RefTests.Approve)]
    public static async Task<RejectRefTestsResult> RejectRefTestsAsync(
        RejectRefTestsInput input,
        [Service] RefTestApprovalHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.RejectAsync(input.Ids, input.Reason, cancellationToken);
        return new RejectRefTestsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullyRejected = result.RejectedRefTests.Count,
            Failed = result.Errors.Count,
            RejectedRefTests = result.RejectedRefTests.Select(refTest => refTest.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new RejectRefTestsError
            {
                RefTestId = error.RefTestId,
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }
}
