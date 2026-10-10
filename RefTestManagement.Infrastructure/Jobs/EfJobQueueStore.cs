using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Jobs;

public sealed class EfJobQueueStore(
    RefTestManagementContext context,
    ILogger<EfJobQueueStore> logger) : IJobQueueStore
{
    private const string ExhaustedLeaseMessage =
        "Job processing lease expired after all attempts were exhausted.";

    public async Task<IReadOnlyList<Guid>> GetReadyJobIdsAsync(
        DateTime now,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken) =>
        await context.Jobs
            .Where(job => (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
                          && job.ExecuteAfter <= now
                          && job.Attempts < maxAttempts
                          && (job.Status == JobStatus.Pending
                              || job.Attempts + 1 < maxAttempts)
                          && (job.LockedUntil == null || job.LockedUntil <= now))
            .OrderBy(job => job.ExecuteAfter)
            .Take(batchSize)
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExpiredFinalAttemptJob>> FailExpiredFinalAttemptJobsAsync(
        DateTime now,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var candidateIds = await context.Jobs
            .Where(job => job.Status == JobStatus.Processing
                          && job.Attempts + 1 >= maxAttempts
                          && (job.LockedUntil == null || job.LockedUntil <= now))
            .OrderBy(job => job.LockedUntil)
            .Take(batchSize)
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);

        var failedJobs = new List<ExpiredFinalAttemptJob>();
        foreach (var candidateId in candidateIds)
        {
            try
            {
                var job = await context.Jobs
                    .SingleOrDefaultAsync(candidate => candidate.Id == candidateId, cancellationToken);
                if (job is null
                    || !job.MarkAsFailedAfterAttemptsExhausted(
                        now,
                        maxAttempts,
                        ExhaustedLeaseMessage))
                    continue;

                await context.SaveChangesWithRetryAsync(cancellationToken);
                failedJobs.Add(new ExpiredFinalAttemptJob(job.Id, job.JobType, job.Attempts));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                context.ChangeTracker.Clear();
                logger.LogError("Could not terminalize exhausted background job {JobId}", candidateId);
            }
        }

        return failedJobs;
    }

    /// <summary>
    /// Takes exclusive ownership of a job with one conditional update, or returns null when a
    /// competing worker has already claimed it.
    /// </summary>
    /// <remarks>
    /// The update is atomic across the configured database providers. It advances the version
    /// because bulk updates bypass the concurrency-token interceptor, then reloads any tracked
    /// entity so later saves cannot overwrite the claim with stale values.
    /// </remarks>
    public async Task<Job?> ClaimJobAsync(
        Guid jobId,
        int maxAttempts,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lockedUntil = now.Add(lockDuration);

        var claimed = await context.Jobs
            .Where(job => job.Id == jobId
                          && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
                          && job.ExecuteAfter <= now
                          && job.Attempts < maxAttempts
                          && (job.Status == JobStatus.Pending
                              || job.Attempts + 1 < maxAttempts)
                          && (job.LockedUntil == null || job.LockedUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        job => job.Attempts,
                        job => job.Status == JobStatus.Processing ? job.Attempts + 1 : job.Attempts)
                    .SetProperty(job => job.Status, JobStatus.Processing)
                    .SetProperty(job => job.LockedUntil, lockedUntil)
                    .SetProperty(job => job.Version, job => job.Version + 1),
                cancellationToken);

        if (claimed == 0)
            return null;

        var tracked = context.ChangeTracker.Entries<Job>()
            .FirstOrDefault(entry => entry.Entity.Id == jobId);

        if (tracked is not null)
        {
            await tracked.ReloadAsync(cancellationToken);
            return tracked.Entity;
        }

        return await context.Jobs.FirstOrDefaultAsync(job => job.Id == jobId, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesWithRetryAsync(cancellationToken);

    public async Task<int> DeleteOldJobsAsync(
        DateTime completedCutoff,
        DateTime failedCutoff,
        CancellationToken cancellationToken)
    {
        var oldJobs = await context.Jobs
            .Where(job =>
                (job.Status == JobStatus.Completed && job.CompletedAt != null && job.CompletedAt < completedCutoff) ||
                ((job.Status == JobStatus.Failed || job.Status == JobStatus.Cancelled)
                 && job.CompletedAt != null && job.CompletedAt < failedCutoff))
            .ToListAsync(cancellationToken);

        if (oldJobs.Count == 0)
            return 0;

        context.Jobs.RemoveRange(oldJobs);
        await context.SaveChangesWithRetryAsync(cancellationToken);
        return oldJobs.Count;
    }
}
