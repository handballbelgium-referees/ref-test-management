using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>
/// The transaction boundary of one RefTest write use case. RefTests and the jobs they owe are
/// staged here and committed together by <see cref="SaveChangesAsync"/>, so a persisted RefTest
/// can never exist without its invitation or approval-notification job.
/// </summary>
public interface IRefTestUnitOfWork
{
    /// <summary>Loads the RefTests with the given ids that exist, tracked for update.</summary>
    Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void AddRefTests(IEnumerable<RefTest> refTests);

    /// <summary>
    /// Throws away the unsaved changes made to <paramref name="refTest"/> and returns its persisted
    /// state, or <see langword="null"/> when it no longer exists.
    /// </summary>
    Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken);

    /// <summary>Stages the invitation email job for <paramref name="refTest"/>; nothing is saved.</summary>
    Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken);

    /// <summary>Stages the approval notification job; nothing is saved.</summary>
    Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken);

    /// <summary>Stages the approval decision email job; nothing is saved.</summary>
    Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken);

    /// <summary>
    /// Starts tracking jobs staged from now on, so a staging attempt that fails part-way can drop
    /// exactly its own jobs and leave the rest of the unit of work intact.
    /// </summary>
    /// <returns>A callback that removes the jobs staged since this call from the unit of work.</returns>
    Action BeginJobStaging();

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
