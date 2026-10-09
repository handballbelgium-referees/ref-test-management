using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

/// <param name="Deleted">Snapshots taken before each RefTest was erased, in request order.</param>
public sealed record DeleteRefTestsOutcome<TSnapshot>(
    IReadOnlyList<TSnapshot> Deleted,
    IReadOnlyList<RefTestItemFailure> Failures);

/// <summary>
/// Permanently deletes RefTests on an authorized staff request. Each RefTest's personal data is
/// redacted from its audit trail and the row removed in one transaction (see
/// <see cref="IRefTestPrivacyErasureService.EraseAndDeleteAsync"/>), so nothing personal is left behind.
/// </summary>
public static class RefTestDeletionHandler
{
    /// <param name="snapshot">
    /// Captures what the caller wants to return for a deleted RefTest. It runs before erasure,
    /// because erasure anonymizes the entity in memory and would leave only redacted placeholders.
    /// </param>
    public static async Task<DeleteRefTestsOutcome<TSnapshot>> HandleAsync<TSnapshot>(
        IReadOnlyList<Guid> ids,
        Func<RefTest, TSnapshot> snapshot,
        IRefTestUnitOfWork unitOfWork,
        IRefTestPrivacyErasureService erasureService,
        IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        var refTests = await unitOfWork.GetRefTestsAsync([.. ids], cancellationToken);
        var deleted = new List<(TSnapshot Snapshot, Guid Id, RefTestStatus Status)>();
        var failures = new List<RefTestItemFailure>();

        foreach (var id in ids)
        {
            try
            {
                var refTest = refTests.FirstOrDefault(rt => rt.Id == id) ?? throw new RefTestNotFoundException(id);
                var captured = (snapshot(refTest), refTest.Id, refTest.Status);

                await erasureService.EraseAndDeleteAsync(refTest, ErasureInitiator.Operator, cancellationToken);
                deleted.Add(captured);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failures.Add(new RefTestItemFailure(id, ex));
            }
        }

        foreach (var (_, id, status) in deleted)
            await subscriptionService.PublishRefTestDeletedAsync(id, status, cancellationToken);

        return new DeleteRefTestsOutcome<TSnapshot>([.. deleted.Select(item => item.Snapshot)], failures);
    }
}
