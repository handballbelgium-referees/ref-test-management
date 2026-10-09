using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

/// <summary>A requested RefTest that was not approved or rejected, and why.</summary>
/// <param name="DuringInvitation">The decision itself was valid but the invitation could not be staged.</param>
public sealed record RefTestDecisionFailure(Guid RefTestId, Exception Exception, bool DuringInvitation = false);

/// <param name="Approved">Approved RefTests, in request order.</param>
public sealed record ApproveRefTestsOutcome(
    IReadOnlyList<RefTest> Approved,
    IReadOnlyList<RefTestDecisionFailure> Failures);

/// <param name="Rejected">Rejected RefTests, in request order.</param>
public sealed record RejectRefTestsOutcome(
    IReadOnlyList<RefTest> Rejected,
    IReadOnlyList<RefTestDecisionFailure> Failures);

/// <summary>
/// Approves RefTests awaiting approval. An approved RefTest that sends invitations automatically
/// gets a fresh token and an invitation job; if that job cannot be staged the approval is undone
/// for that RefTest only. Every creator is sent one decision email, and everything is saved once.
/// </summary>
public sealed class ApproveRefTestsHandler(IRefTestSubscriptionService subscriptionService, TimeProvider timeProvider)
{
    public async Task<ApproveRefTestsOutcome> HandleAsync(
        IReadOnlyList<Guid> ids,
        string approverName,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var refTests = (await unitOfWork.GetRefTestsAsync([.. ids], cancellationToken)).ToList();
        var approved = new List<RefTest>();
        var oldStatuses = new Dictionary<Guid, RefTestStatus>();
        var failures = new List<RefTestDecisionFailure>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var id in ids)
        {
            RefTest? refTest = null;
            var discard = unitOfWork.BeginJobStaging();
            var stagingInvitation = false;
            try
            {
                refTest = refTests.FirstOrDefault(rt => rt.Id == id) ?? throw new RefTestNotFoundException(id);

                if (oldStatuses.ContainsKey(refTest.Id))
                    throw new InvalidOperationException("A RefTest can only be approved once per request.");

                var oldStatus = refTest.Status;
                refTest.Approve(now);

                if (refTest.SendInvitationsAutomatically)
                {
                    refTest.RegenerateToken();
                    stagingInvitation = true;
                    await unitOfWork.StageInvitationEmailAsync(refTest, refTest.ScheduledAt, cancellationToken);
                    stagingInvitation = false;
                }

                approved.Add(refTest);
                oldStatuses.Add(refTest.Id, oldStatus);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // A RefTest already approved earlier in this request keeps that approval.
                if (refTest is not null && !oldStatuses.ContainsKey(refTest.Id))
                {
                    discard();
                    var index = refTests.IndexOf(refTest);
                    var restored = await unitOfWork.RevertRefTestAsync(refTest, cancellationToken);
                    if (restored is null)
                        refTests.RemoveAt(index);
                    else
                        refTests[index] = restored;
                }

                failures.Add(new RefTestDecisionFailure(id, ex, stagingInvitation));
            }
        }

        if (approved.Count == 0)
            return new ApproveRefTestsOutcome(approved, failures);

        await StageDecisionEmailsAsync(approved, approverName, isApproved: true, rejectionReason: null,
            unitOfWork, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var refTest in approved)
            await subscriptionService.PublishRefTestApprovedAsync(
                refTest.Id, oldStatuses[refTest.Id], refTest.Status, now, refTest.CreatedAt, cancellationToken);

        return new ApproveRefTestsOutcome(approved, failures);
    }

    /// <summary>Stages one decision email per distinct creator, committed together with the decisions.</summary>
    internal static async Task StageDecisionEmailsAsync(
        IReadOnlyList<RefTest> decided,
        string approverName,
        bool isApproved,
        string? rejectionReason,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        foreach (var creatorGroup in decided.GroupBy(rt => rt.CreatorEmail))
        {
            var first = creatorGroup.First();
            if (string.IsNullOrWhiteSpace(first.CreatorEmail))
                continue;

            await unitOfWork.StageApprovalDecisionEmailAsync(
                new ApprovalDecisionEmailPayload(
                    first.CreatorName, first.CreatorEmail,
                    approverName, isApproved,
                    RejectionReason: rejectionReason,
                    TitleValue: null,
                    [.. creatorGroup.Select(rt => new ApprovalNotificationRefTestItem(
                        rt.Id, rt.FirstName, rt.LastName, rt.Email, rt.ScheduledAt))]),
                cancellationToken);
        }
    }
}

/// <summary>
/// Rejects RefTests awaiting approval with a required reason. Every creator is sent one decision
/// email, and everything is saved once.
/// </summary>
public sealed class RejectRefTestsHandler(IRefTestSubscriptionService subscriptionService, TimeProvider timeProvider)
{
    public async Task<RejectRefTestsOutcome> HandleAsync(
        IReadOnlyList<Guid> ids,
        string reason,
        string approverName,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var refTests = await unitOfWork.GetRefTestsAsync([.. ids], cancellationToken);
        var rejected = new List<RefTest>();
        var failures = new List<RefTestDecisionFailure>();

        foreach (var id in ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id) ?? throw new RefTestNotFoundException(id);

                // Reject validates before it changes anything, so a failure leaves nothing to undo.
                refTest.Reject(reason);
                rejected.Add(refTest);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failures.Add(new RefTestDecisionFailure(id, ex));
            }
        }

        if (rejected.Count == 0)
            return new RejectRefTestsOutcome(rejected, failures);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await ApproveRefTestsHandler.StageDecisionEmailsAsync(rejected, approverName, isApproved: false,
            rejectionReason: reason, unitOfWork, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var refTest in rejected)
            await subscriptionService.PublishRefTestRejectedAsync(refTest.Id, refTest.Status, reason, now, cancellationToken);

        return new RejectRefTestsOutcome(rejected, failures);
    }
}
