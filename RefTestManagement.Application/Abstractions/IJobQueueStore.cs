using Handball.Belgium.RefTestManagement.Domain.Jobs;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public sealed record ExpiredFinalAttemptJob(Guid JobId, JobType JobType, int Attempts);

public interface IJobQueueStore
{
    /// <summary>Reads eligible IDs in execution order without loading job payloads.</summary>
    Task<IReadOnlyList<Guid>> GetReadyJobIdsAsync(
        DateTime now,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fails expired processing leases that have no attempts left. The returned jobs were
    /// terminalized and saved; per-job persistence failures are logged and skipped.
    /// </summary>
    Task<IReadOnlyList<ExpiredFinalAttemptJob>> FailExpiredFinalAttemptJobsAsync(
        DateTime now,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims an eligible job or returns null when another worker owns it.
    /// </summary>
    Task<Job?> ClaimJobAsync(
        Guid jobId,
        int maxAttempts,
        TimeSpan lockDuration,
        CancellationToken cancellationToken);

    /// <summary>Saves changes made to a claimed job by its handler or the worker.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Deletes completed and terminal jobs past their separate retention cutoffs.</summary>
    Task<int> DeleteOldJobsAsync(
        DateTime completedCutoff,
        DateTime failedCutoff,
        CancellationToken cancellationToken);
}
