using Handball.Belgium.RefTestManagement.Domain.Jobs;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;

/// <summary>Creation and deletion persistence operations over the shared scoped job unit of work.</summary>
public interface IUnitOfWork : IJobPersistenceContext
{
    IReadOnlySet<Guid> CaptureStagedJobIds();
    void DiscardJobsStagedSince(IReadOnlySet<Guid> checkpoint);
}
