using Handball.Belgium.RefTestManagement.Api.Extensions;
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
public static class RefTestApprovalMutations
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
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    [Authorize(Policy = Permissions.RefTests.Approve)]
    public static async Task<ApproveRefTestsResult> ApproveRefTestsAsync(
        ApproveRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var approverName = (httpContextAccessor.HttpContext?.User).GetDisplayName();

        var refTests = await context.RefTests
            .Where(rt => input.Ids.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        var approved = new List<RefTest>();
        var errors = new List<ApproveRefTestsError>();

        foreach (var id in input.Ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id)
                              ?? throw new RefTestNotFoundException(id);

                refTest.Approve();
                approved.Add(refTest);
            }
            catch (Exception ex)
            {
                errors.Add(new ApproveRefTestsError { RefTestId = id, ErrorMessage = ex.Message });
            }
        }

        if (approved.Count <= 0)
            return new ApproveRefTestsResult
            {
                TotalRequested = input.Ids.Count,
                SuccessfullyApproved = approved.Count,
                Failed = errors.Count(e => approved.All(r => r.Id != e.RefTestId)),
                ApprovedRefTests = approved.Select(r => r.ToDto()).ToList(),
                Errors = errors
            };

        await context.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var refTest in approved)
        {
            // Enqueue invitation email if the RefTest is configured for automated invitations
            if (refTest.SendInvitationsAutomatically)
            {
                try
                {
                    await jobEnqueueService.EnqueueInvitationEmailAsync(
                        new InvitationEmailPayload(
                            refTest.Id,
                            refTest.FullName,
                            refTest.Email,
                            refTest.Token,
                            refTest.NumberOfQuestions,
                            refTest.MaxTimeInMinutes),
                        executeAfter: refTest.ScheduledAt,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    errors.Add(new ApproveRefTestsError
                    {
                        RefTestId = refTest.Id,
                        ErrorMessage = $"RefTest approved but invitation email enqueue failed: {ex.Message}"
                    });
                }
            }

            await subscriptionService.PublishRefTestApprovedAsync(
                refTest.Id, refTest.Status, now, cancellationToken);
        }

        // Send decision confirmation email to each distinct creator
        foreach (var creatorGroup in approved.GroupBy(rt => rt.CreatorEmail))
        {
            var first = creatorGroup.First();
            if (string.IsNullOrWhiteSpace(first.CreatorEmail)) continue;

            var items = creatorGroup.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id, rt.FirstName, rt.LastName, rt.Email)).ToList();

            await jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
                new ApprovalDecisionEmailPayload(
                    first.CreatorName, first.CreatorEmail,
                    approverName, IsApproved: true,
                    RejectionReason: null,
                    TitleValue: null,
                    items),
                cancellationToken);
        }

        return new ApproveRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullyApproved = approved.Count,
            Failed = errors.Count(e => approved.All(r => r.Id != e.RefTestId)),
            ApprovedRefTests = approved.Select(r => r.ToDto()).ToList(),
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
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    [Authorize(Policy = Permissions.RefTests.Approve)]
    public static async Task<RejectRefTestsResult> RejectRefTestsAsync(
        RejectRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var approverName = (httpContextAccessor.HttpContext?.User).GetDisplayName();

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
                errors.Add(new RejectRefTestsError { RefTestId = id, ErrorMessage = ex.Message });
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

        await context.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var refTest in rejected)
        {
            await subscriptionService.PublishRefTestRejectedAsync(
                refTest.Id, refTest.Status, input.Reason, now, cancellationToken);
        }

        // Send decision confirmation email to each distinct creator
        foreach (var creatorGroup in rejected.GroupBy(rt => rt.CreatorEmail))
        {
            var first = creatorGroup.First();
            if (string.IsNullOrWhiteSpace(first.CreatorEmail)) continue;

            var items = creatorGroup.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id, rt.FirstName, rt.LastName, rt.Email)).ToList();

            await jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
                new ApprovalDecisionEmailPayload(
                    first.CreatorName, first.CreatorEmail,
                    approverName, IsApproved: false,
                    RejectionReason: input.Reason,
                    TitleValue: null,
                    items),
                cancellationToken);
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