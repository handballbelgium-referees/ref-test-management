using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

/// <summary>A requested RefTest that could not be reset or revived, and why.</summary>
public sealed record RefTestItemFailure(Guid RefTestId, Exception Exception);

/// <param name="RefTest">The RefTest after it was saved.</param>
/// <param name="OldStatus">The status before the reset.</param>
public sealed record ResetRefTest(RefTest RefTest, RefTestStatus OldStatus);

/// <param name="Reset">Reset RefTests, in request order.</param>
public sealed record ResetRefTestsOutcome(IReadOnlyList<ResetRefTest> Reset, IReadOnlyList<RefTestItemFailure> Failures);

/// <param name="Revived">Revived RefTests, in request order.</param>
public sealed record ReviveRefTestsOutcome(IReadOnlyList<RefTest> Revived, IReadOnlyList<RefTestItemFailure> Failures);

/// <summary>
/// Resets RefTests so they can be taken again. Each RefTest is saved on its own: a failure rolls
/// back only that RefTest's changes, including the jobs cancelled or staged for it.
/// </summary>
public sealed class ResetRefTestsHandler(IRefTestSubscriptionService subscriptionService, TimeProvider timeProvider)
{
    public async Task<ResetRefTestsOutcome> HandleAsync(
        IReadOnlyList<Guid> ids,
        RefTestResetType resetType,
        bool regenerateToken,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var refTests = (await unitOfWork.GetRefTestsAsync([.. ids], cancellationToken)).ToList();
        var reset = new List<ResetRefTest>();
        var failures = new List<RefTestItemFailure>();

        // A hard reset always issues a new token; a soft reset only when asked to.
        var willRegenerateToken = resetType == RefTestResetType.Hard || regenerateToken;

        foreach (var id in ids)
        {
            RefTest? refTest = null;
            try
            {
                refTest = refTests.FirstOrDefault(rt => rt.Id == id) ?? throw new RefTestNotFoundException(id);

                // Results are cleared by both kinds of reset, so result emails always go. Invitation
                // and expiration jobs only go when the token they carry is replaced.
                if (willRegenerateToken)
                    await unitOfWork.CancelPendingJobsAsync(id, cancellationToken);
                else
                    await unitOfWork.CancelPendingResultEmailsAsync(id, cancellationToken);

                var invitationWasSent = refTest.InvitationSentAt.HasValue;
                var oldStatus = refTest.Status;

                if (resetType == RefTestResetType.Soft)
                    refTest.SoftReset(regenerateToken);
                else
                    refTest.HardReset(timeProvider.GetUtcNow().UtcDateTime);

                // A participant who already had an invitation needs one carrying the new token.
                if (invitationWasSent && willRegenerateToken)
                    await unitOfWork.StageInvitationEmailAsync(refTest, executeAfter: null, cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                reset.Add(new ResetRefTest(refTest, oldStatus));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (refTest is not null)
                    await ReplaceWithPersistedAsync(refTests, refTest, unitOfWork, cancellationToken);
                failures.Add(new RefTestItemFailure(id, ex));
            }
        }

        foreach (var item in reset)
            await subscriptionService.PublishRefTestResetAsync(
                item.RefTest.Id, item.OldStatus, resetType, item.RefTest.Status, item.RefTest.CreatedAt,
                item.RefTest.InvitationSentAt.HasValue, cancellationToken);

        return new ResetRefTestsOutcome(reset, failures);
    }

    /// <summary>Rolls back a failed item and swaps in its persisted state for any later duplicate id.</summary>
    internal static async Task ReplaceWithPersistedAsync(
        List<RefTest> refTests,
        RefTest refTest,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var restored = await unitOfWork.DiscardChangesAsync(refTest, cancellationToken);
        var index = refTests.IndexOf(refTest);
        if (index < 0)
            return;

        if (restored is null)
            refTests.RemoveAt(index);
        else
            refTests[index] = restored;
    }
}

/// <summary>
/// Revives expired RefTests: back to Pending with a new token and a fresh expiration timer. Each
/// RefTest is saved on its own, and a failure rolls back only that RefTest's changes.
/// </summary>
public sealed class ReviveRefTestsHandler(IRefTestSubscriptionService subscriptionService, TimeProvider timeProvider)
{
    public async Task<ReviveRefTestsOutcome> HandleAsync(
        IReadOnlyList<Guid> ids,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var refTests = (await unitOfWork.GetRefTestsAsync([.. ids], cancellationToken)).ToList();
        var revived = new List<RefTest>();
        var failures = new List<RefTestItemFailure>();

        foreach (var id in ids)
        {
            var refTest = refTests.FirstOrDefault(rt => rt.Id == id);
            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id);

                // Pending jobs carry the old token, old scores or an outdated expiry check.
                await unitOfWork.CancelPendingJobsAsync(id, cancellationToken);

                var invitationWasSent = refTest.InvitationSentAt.HasValue;
                refTest.Revive(timeProvider.GetUtcNow().UtcDateTime);

                // Revive always issues a new token, so a participant who had an invitation needs a new one.
                if (invitationWasSent)
                    await unitOfWork.StageInvitationEmailAsync(refTest, executeAfter: null, cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                revived.Add(refTest);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (refTest is not null)
                    await ResetRefTestsHandler.ReplaceWithPersistedAsync(refTests, refTest, unitOfWork, cancellationToken);
                failures.Add(new RefTestItemFailure(id, ex));
            }
        }

        foreach (var refTest in revived)
            await subscriptionService.PublishRefTestRevivedAsync(
                refTest.Id, refTest.Status, refTest.CreatedAt, refTest.InvitationSentAt.HasValue, cancellationToken);

        return new ReviveRefTestsOutcome(revived, failures);
    }
}
