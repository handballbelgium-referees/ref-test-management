using Handball.Belgium.RefTestManagement.Domain.Jobs;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>
/// The unit of work a job is staged into. Jobs added here are written by the same
/// <see cref="SaveChangesWithRetryAsync"/> call as any other change tracked by that unit of work,
/// so an entity and the job it owes commit together or not at all.
/// </summary>
public interface IJobPersistenceContext
{
    /// <summary>Queryable view of persisted jobs.</summary>
    IQueryable<Job> Jobs { get; }

    /// <summary>Stages a new job; it is written by the next <see cref="SaveChangesWithRetryAsync"/>.</summary>
    void AddJob(Job job);

    Task<int> SaveChangesWithRetryAsync(CancellationToken cancellationToken = default);
}
