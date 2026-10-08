using Handball.Belgium.RefTestManagement.Api.Extensions;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

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
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestApprovalMutations));
        var approverName = (httpContextAccessor.HttpContext?.User).GetDisplayName();
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var refTests = await context.RefTests
            .Where(rt => input.Ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var approved = new List<RefTest>();
        var approvedOldStatuses = new Dictionary<Guid, RefTestStatus>();
        var errors = new List<ApproveRefTestsError>();

        foreach (var id in input.Ids)
        {
            RefTest? refTest = null;
            var trackedJobIds = context.Jobs.Local.Select(job => job.Id).ToHashSet();
            var failedPreparingInvitation = false;
            try
            {
                refTest = refTests.FirstOrDefault(rt => rt.Id == id)
                              ?? throw new RefTestNotFoundException(id);

                if (approvedOldStatuses.ContainsKey(refTest.Id))
                    throw new InvalidOperationException("A RefTest can only be approved once per request.");

                var oldStatus = refTest.Status;
                refTest.Approve();

                if (refTest.SendInvitationsAutomatically)
                {
                    refTest.RegenerateToken();
                    failedPreparingInvitation = true;
                    await jobEnqueueService.EnqueueInvitationEmailAsync(
                        refTest,
                        executeAfter: refTest.ScheduledAt,
                        saveChanges: false,
                        unitOfWorkContext: context,
                        cancellationToken: cancellationToken);
                    failedPreparingInvitation = false;
                }

                approved.Add(refTest);
                approvedOldStatuses.Add(refTest.Id, oldStatus);
            }
            catch (Exception ex)
            {
                if (refTest is not null && !approvedOldStatuses.ContainsKey(refTest.Id))
                {
                    var restoredRefTest = await RollbackFailedApprovalAsync(
                        context, refTest, trackedJobIds, cancellationToken);
                    var refTestIndex = refTests.IndexOf(refTest);
                    if (refTestIndex >= 0)
                    {
                        if (restoredRefTest is null)
                            refTests.RemoveAt(refTestIndex);
                        else
                            refTests[refTestIndex] = restoredRefTest;
                    }
                }

                errors.Add(new ApproveRefTestsError
                {
                    RefTestId = id,
                    ErrorMessage = failedPreparingInvitation
                        ? $"Invitation email could not be prepared: {MutationErrorHandling.GetUserSafeMessage(ex)}"
                        : MutationErrorHandling.GetUserSafeMessage(ex)
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(ApproveRefTestsAsync), correlationId, id);
            }
        }

        if (approved.Count <= 0)
            return new ApproveRefTestsResult
            {
                TotalRequested = input.Ids.Count,
                SuccessfullyApproved = approved.Count,
                Failed = input.Ids.Count - approved.Count,
                ApprovedRefTests = approved.Select(r => r.ToDto()).ToList(),
                Errors = errors
            };

        var now = DateTime.UtcNow;

        // Stage a decision confirmation email for each distinct creator
        foreach (var creatorGroup in approved.GroupBy(rt => rt.CreatorEmail))
        {
            var first = creatorGroup.First();
            if (string.IsNullOrWhiteSpace(first.CreatorEmail)) continue;

            var items = creatorGroup.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id, rt.FirstName, rt.LastName, rt.Email, rt.ScheduledAt)).ToList();

            await jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
                new ApprovalDecisionEmailPayload(
                    first.CreatorName, first.CreatorEmail,
                    approverName, IsApproved: true,
                    RejectionReason: null,
                    TitleValue: null,
                    items),
                saveChanges: false,
                unitOfWorkContext: context,
                cancellationToken: cancellationToken);
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        foreach (var refTest in approved)
        {
            await subscriptionService.PublishRefTestApprovedAsync(
                refTest.Id, approvedOldStatuses[refTest.Id], refTest.Status, now, refTest.CreatedAt,
                cancellationToken);
        }

        return new ApproveRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullyApproved = approved.Count,
            Failed = input.Ids.Count - approved.Count,
            ApprovedRefTests = approved.Select(r => r.ToDto()).ToList(),
            Errors = errors
        };
    }

    /// <summary>
    /// Discards the failed approval's staged invitation job and restores a clean RefTest instance.
    /// </summary>
    /// <param name="context">The RefTest unit of work.</param>
    /// <param name="refTest">The aggregate modified by the failed approval attempt.</param>
    /// <param name="trackedJobIds">Job IDs already staged before the item was processed.</param>
    /// <param name="cancellationToken">Token for the cleanup query.</param>
    private static async Task<RefTest?> RollbackFailedApprovalAsync(
        RefTestManagementContext context,
        RefTest refTest,
        HashSet<Guid> trackedJobIds,
        CancellationToken cancellationToken)
    {
        var refTestId = refTest.Id;

        foreach (var job in context.Jobs.Local.Where(job => !trackedJobIds.Contains(job.Id)).ToList())
            context.Entry(job).State = EntityState.Detached;

        refTest.ClearDomainEvents();
        context.Entry(refTest).State = EntityState.Detached;
        return await context.RefTests.FirstOrDefaultAsync(candidate => candidate.Id == refTestId, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestApprovalMutations));
        var approverName = (httpContextAccessor.HttpContext?.User).GetDisplayName();
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var refTests = await context.RefTests
            .Where(rt => input.Ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var rejected = new List<RefTest>();
        var errors = new List<RejectRefTestsError>();

        foreach (var id in input.Ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id)
                              ?? throw new RefTestNotFoundException(id);

                refTest.Reject(input.Reason);
                rejected.Add(refTest);
            }
            catch (Exception ex)
            {
                errors.Add(new RejectRefTestsError
                {
                    RefTestId = id,
                    ErrorMessage = MutationErrorHandling.GetUserSafeMessage(ex)
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(RejectRefTestsAsync), correlationId, id);
            }
        }

        if (rejected.Count <= 0)
            return new RejectRefTestsResult
            {
                TotalRequested = input.Ids.Count,
                SuccessfullyRejected = rejected.Count,
                Failed = errors.Count,
                RejectedRefTests = rejected.Select(r => r.ToDto()).ToList(),
                Errors = errors
            };

        var now = DateTime.UtcNow;

        // Stage a decision confirmation email for each distinct creator, committed together with
        // the rejections themselves.
        foreach (var creatorGroup in rejected.GroupBy(rt => rt.CreatorEmail))
        {
            var first = creatorGroup.First();
            if (string.IsNullOrWhiteSpace(first.CreatorEmail)) continue;

            var items = creatorGroup.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id, rt.FirstName, rt.LastName, rt.Email, rt.ScheduledAt)).ToList();

            await jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
                new ApprovalDecisionEmailPayload(
                    first.CreatorName, first.CreatorEmail,
                    approverName, IsApproved: false,
                    RejectionReason: input.Reason,
                    TitleValue: null,
                    items),
                saveChanges: false,
                unitOfWorkContext: context,
                cancellationToken: cancellationToken);
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        foreach (var refTest in rejected)
        {
            await subscriptionService.PublishRefTestRejectedAsync(
                refTest.Id, refTest.Status, input.Reason, now, cancellationToken);
        }

        return new RejectRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullyRejected = rejected.Count,
            Failed = errors.Count,
            RejectedRefTests = rejected.Select(r => r.ToDto()).ToList(),
            Errors = errors
        };
    }
}
