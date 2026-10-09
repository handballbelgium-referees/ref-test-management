using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;

/// <summary>RefTest and job persistence operations over the shared scoped unit of work.</summary>
public interface IUnitOfWork : IJobPersistenceContext
{
    IReadOnlySet<Guid> CaptureStagedJobIds();
    void DiscardJobsStagedSince(IReadOnlySet<Guid> checkpoint);
    Task<RefTest?> RestoreRefTestAsync(RefTest refTest, CancellationToken cancellationToken = default);
    Task<RefTest?> RestoreChangesAsync(RefTest refTest, CancellationToken cancellationToken = default);
    bool IsConcurrencyException(Exception exception);
}
