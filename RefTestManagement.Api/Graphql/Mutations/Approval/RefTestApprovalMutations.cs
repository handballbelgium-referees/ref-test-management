using Handball.Belgium.RefTestManagement.Api.Extensions;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
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
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="subscriptionService">Service for managing subscriptions to RefTest approval events.</param>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    [Authorize(Policy = Permissions.RefTests.Approve)]
    public static async Task<ApproveRefTestsResult> ApproveRefTestsAsync(
        ApproveRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestApprovalMutations));
        var approverName = (httpContextAccessor.HttpContext?.User).GetDisplayName();
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var outcome = await new ApproveRefTestsHandler(subscriptionService, timeProvider ?? TimeProvider.System)
            .HandleAsync(input.Ids, approverName, new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);

        var errors = new List<ApproveRefTestsError>();
        foreach (var failure in outcome.Failures)
        {
            var message = MutationErrorHandling.GetUserSafeMessage(failure.Exception);
            errors.Add(new ApproveRefTestsError
            {
                RefTestId = failure.RefTestId,
                ErrorMessage = failure.DuringInvitation ? $"Invitation email could not be prepared: {message}" : message
            });
            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(ApproveRefTestsAsync), correlationId, failure.RefTestId);
        }

        return new ApproveRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullyApproved = outcome.Approved.Count,
            Failed = input.Ids.Count - outcome.Approved.Count,
            ApprovedRefTests = outcome.Approved.Select(r => r.ToDto()).ToList(),
            Errors = errors
        };
    }
    /// <summary>
    /// Reject one or more RefTests that are pending approval.
    /// A non-empty rejection reason is required.
    /// </summary>
    /// <param name="input">The input parameters for RefTest rejection.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="subscriptionService">Service for managing subscriptions to RefTest rejection events.</param>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    [Authorize(Policy = Permissions.RefTests.Approve)]
    public static async Task<RejectRefTestsResult> RejectRefTestsAsync(
        RejectRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestApprovalMutations));
        var approverName = (httpContextAccessor.HttpContext?.User).GetDisplayName();
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var outcome = await new RejectRefTestsHandler(subscriptionService, timeProvider ?? TimeProvider.System)
            .HandleAsync(input.Ids, input.Reason, approverName, new EfRefTestUnitOfWork(context, jobEnqueueService),
                cancellationToken);

        var errors = new List<RejectRefTestsError>();
        foreach (var failure in outcome.Failures)
        {
            errors.Add(new RejectRefTestsError
            {
                RefTestId = failure.RefTestId,
                ErrorMessage = MutationErrorHandling.GetUserSafeMessage(failure.Exception)
            });
            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(RejectRefTestsAsync), correlationId, failure.RefTestId);
        }

        return new RejectRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullyRejected = outcome.Rejected.Count,
            Failed = errors.Count,
            RejectedRefTests = outcome.Rejected.Select(r => r.ToDto()).ToList(),
            Errors = errors
        };
    }
}