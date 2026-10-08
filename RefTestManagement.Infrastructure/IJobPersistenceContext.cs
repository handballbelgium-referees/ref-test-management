using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure;

public interface IJobPersistenceContext
{
    DbSet<Job> Jobs { get; }

    void DetachJob(Job job);

    Task<int> SaveChangesWithRetryAsync(CancellationToken cancellationToken = default);
}
