using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Approval;

public sealed class RefTestApprovalHandler(
    IRefTestRepository refTestRepository,
    IUnitOfWork unitOfWork,
    IJobEnqueueService jobEnqueueService,
    IRefTestSubscriptionService subscriptionService,
    ICurrentUser currentUser,
    ILogger<RefTestApprovalHandler> logger)
{
    public async Task<ApproveRefTestsResult> ApproveAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetByIdsAsync(ids, cancellationToken);
        var approved = new List<RefTest>();
        var oldStatuses = new Dictionary<Guid, RefTestStatus>();
        var errors = new List<ApproveRefTestsError>();

        foreach (var id in ids)
        {
            RefTest? refTest = null;
            var checkpoint = unitOfWork.CaptureStagedJobIds();
            var failedPreparingInvitation = false;
            try
            {
                refTest = refTests.FirstOrDefault(candidate => candidate.Id == id)
                          ?? throw new RefTestNotFoundException(id);
                if (oldStatuses.ContainsKey(refTest.Id))
                    throw new InvalidOperationException("A RefTest can only be approved once per request.");

                var oldStatus = refTest.Status;
                refTest.Approve();
                if (refTest.SendInvitationsAutomatically)
                {
                    refTest.RegenerateToken();
                    failedPreparingInvitation = true;
                    await jobEnqueueService.EnqueueInvitationEmailAsync(
                        refTest, executeAfter: refTest.ScheduledAt, saveChanges: false,
                        unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
                    failedPreparingInvitation = false;
                }

                approved.Add(refTest);
                oldStatuses.Add(refTest.Id, oldStatus);
            }
            catch (Exception exception)
            {
                if (refTest is not null && !oldStatuses.ContainsKey(refTest.Id))
                {
                    unitOfWork.DiscardJobsStagedSince(checkpoint);
                    var restoredRefTest = await unitOfWork.RestoreRefTestAsync(refTest, cancellationToken);
                    var index = refTests.IndexOf(refTest);
                    if (index >= 0)
                    {
                        if (restoredRefTest is null)
                            refTests.RemoveAt(index);
                        else
                            refTests[index] = restoredRefTest;
                    }
                }

                errors.Add(new ApproveRefTestsError(
                    id,
                    failedPreparingInvitation
                        ? $"Invitation email could not be prepared: {MutationFailureHandling.GetUserSafeMessage(exception)}"
                        : MutationFailureHandling.GetUserSafeMessage(exception)));
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, "ApproveRefTestsAsync", currentUser.CorrelationId, id);
            }
        }

        if (approved.Count == 0)
            return new ApproveRefTestsResult(ids.Count, approved, errors);

        var now = DateTime.UtcNow;
        // Stage a decision confirmation email for each distinct creator
        foreach (var creatorGroup in approved.GroupBy(refTest => refTest.CreatorEmail))
        {
            var first = creatorGroup.First();
            if (string.IsNullOrWhiteSpace(first.CreatorEmail))
                continue;
            var items = creatorGroup.Select(refTest => new ApprovalNotificationRefTestItem(
                refTest.Id, refTest.FirstName, refTest.LastName, refTest.Email, refTest.ScheduledAt)).ToList();
            await jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
                new ApprovalDecisionEmailPayload(
                    first.CreatorName, first.CreatorEmail, currentUser.DisplayName,
                    IsApproved: true, RejectionReason: null, TitleValue: null, items),
                saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
        }

        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        foreach (var refTest in approved)
            await subscriptionService.PublishRefTestApprovedAsync(
                refTest.Id, oldStatuses[refTest.Id], refTest.Status, now, refTest.CreatedAt, cancellationToken);
        return new ApproveRefTestsResult(ids.Count, approved, errors);
    }

    public async Task<RejectRefTestsResult> RejectAsync(
        IReadOnlyList<Guid> ids, string reason, CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetByIdsAsync(ids, cancellationToken);
        var rejected = new List<RefTest>();
        var errors = new List<RejectRefTestsError>();
        foreach (var id in ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(candidate => candidate.Id == id)
                              ?? throw new RefTestNotFoundException(id);
                refTest.Reject(reason);
                rejected.Add(refTest);
            }
            catch (Exception exception)
            {
                errors.Add(new RejectRefTestsError(id, MutationFailureHandling.GetUserSafeMessage(exception)));
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, "RejectRefTestsAsync", currentUser.CorrelationId, id);
            }
        }

        if (rejected.Count == 0)
            return new RejectRefTestsResult(ids.Count, rejected, errors);

        var now = DateTime.UtcNow;
        // Stage a decision confirmation email for each distinct creator, committed together with
        // the rejections themselves.
        foreach (var creatorGroup in rejected.GroupBy(refTest => refTest.CreatorEmail))
        {
            var first = creatorGroup.First();
            if (string.IsNullOrWhiteSpace(first.CreatorEmail))
                continue;
            var items = creatorGroup.Select(refTest => new ApprovalNotificationRefTestItem(
                refTest.Id, refTest.FirstName, refTest.LastName, refTest.Email, refTest.ScheduledAt)).ToList();
            await jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
                new ApprovalDecisionEmailPayload(
                    first.CreatorName, first.CreatorEmail, currentUser.DisplayName,
                    IsApproved: false, RejectionReason: reason, TitleValue: null, items),
                saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
        }

        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        foreach (var refTest in rejected)
            await subscriptionService.PublishRefTestRejectedAsync(
                refTest.Id, refTest.Status, reason, now, cancellationToken);
        return new RejectRefTestsResult(ids.Count, rejected, errors);
    }
}
