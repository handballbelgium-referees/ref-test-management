using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Background service that processes jobs from a database queue
/// </summary>
public class BackgroundJobService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundJobService> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly TimeSpan _lockDuration;
    private readonly TimeSpan _startupDelay;
    private readonly int _maxAttempts;
    private readonly int _batchSize;
    private readonly bool _enableCleanup;
    private readonly TimeSpan _cleanupInterval;
    private readonly TimeSpan _retainCompletedJobs;
    private readonly TimeSpan _retainFailedJobs;
    private DateTime _lastCleanupTime;

    private const string MaskingFailedMessage =
        "Job failed. The original error message was withheld because it could not be scrubbed of personal data.";

    public BackgroundJobService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundJobService> logger,
        BackgroundJobConfiguration? configuration = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        configuration ??= new BackgroundJobConfiguration();
        _pollingInterval = TimeSpan.FromSeconds(configuration.PollingIntervalSeconds);
        _lockDuration = TimeSpan.FromMinutes(configuration.LockDurationMinutes);
        _startupDelay = TimeSpan.FromSeconds(configuration.StartupDelaySeconds);
        _maxAttempts = configuration.MaxAttempts;
        _batchSize = configuration.BatchSize;
        _enableCleanup = configuration.EnableCleanup;
        _cleanupInterval = TimeSpan.FromHours(configuration.CleanupIntervalHours);
        _retainCompletedJobs = TimeSpan.FromDays(configuration.RetainCompletedJobsDays);
        _retainFailedJobs = TimeSpan.FromDays(configuration.RetainFailedJobsDays);
        _lastCleanupTime = DateTime.MinValue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceLoggerMessages.LogServiceStarting(_logger, nameof(BackgroundJobService));

        // Wait a bit before the first execution to let the app fully start
        await Task.Delay(_startupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessJobsAsync(stoppingToken);

                // Run cleanup if enabled and due
                if (_enableCleanup && ShouldRunCleanup())
                {
                    await CleanupOldJobsAsync(stoppingToken);
                    _lastCleanupTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                // Anything raised while processing jobs can have travelled through a payload or
                // an email provider response, so mask before the exception reaches a log sink.
                ServiceLoggerMessages.LogServiceError(_logger, LogRedaction.MaskEmails(ex), nameof(BackgroundJobService));
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown, no need to log
                break;
            }
        }

        ServiceLoggerMessages.LogServiceStopping(_logger, nameof(BackgroundJobService));
    }

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();

        // Find jobs that are ready to be processed
        // Note: This query mirrors the logic in Job.IsReadyToProcess() for database-level filtering
        //
        // Processing jobs whose lock has expired are reclaimed too. Nothing else recovers them:
        // a worker that dies, or throws on its way into MarkAsFailed, leaves the row Processing
        // forever, and cleanup only ever deletes Completed/Failed/Cancelled rows.
        //
        // Only the ids are read. The row itself is loaded after the claim succeeds, because
        // claiming is what decides whether this worker may touch it at all.
        var now = DateTime.UtcNow;
        var candidateIds = await context.Jobs
            .Where(j => (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing)
                        && j.ExecuteAfter <= now
                        && j.Attempts < _maxAttempts
                        && (j.LockedUntil == null || j.LockedUntil <= now))
            .OrderBy(j => j.ExecuteAfter)
            .Take(_batchSize)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            ServiceLoggerMessages.LogNoJobsAvailable(_logger);
            return;
        }

        ServiceLoggerMessages.LogJobsFound(_logger, candidateIds.Count);

        foreach (var candidateId in candidateIds.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            var job = await ClaimJobAsync(context, candidateId, _maxAttempts, _lockDuration, cancellationToken);

            // Lost the race — another instance holds the lock now.
            if (job is null)
                continue;

            await ProcessJobAsync(job, scope.ServiceProvider, context, _logger, _maxAttempts, cancellationToken);
        }
    }

    /// <summary>
    /// Takes exclusive ownership of a job, or returns <c>null</c> if another worker got there
    /// first.
    /// </summary>
    /// <remarks>
    /// The claim is a single conditional <c>UPDATE</c>. Reading a job and then locking it in a
    /// second round trip lets two instances read the same row before either persists the lock,
    /// which sends the same invitation or result email twice. Here the eligibility test and the
    /// lock are one statement, so the database decides the winner: the loser's <c>UPDATE</c>
    /// matches no rows because the row no longer satisfies the predicate.
    ///
    /// <c>ExecuteUpdateAsync</c> is used rather than a provider-specific <c>UPDATE … OUTPUT</c> or
    /// <c>RETURNING</c>, because the application supports SQL Server, PostgreSQL, MySQL and SQLite
    /// and MySQL has neither. The affected-row count carries the same information.
    ///
    /// The attempt counter mirrors <see cref="Job.MarkAsProcessing"/>: re-claiming a row that is
    /// already <see cref="JobStatus.Processing"/> means its previous lock expired without the
    /// worker reporting back, so that counts as a spent attempt. The <c>CASE</c> reads the
    /// pre-update status, which is what an SQL <c>UPDATE</c> guarantees.
    ///
    /// Static and <c>internal</c> so the race can be exercised directly from tests against a real
    /// provider — the translation of that <c>CASE</c> is the part worth proving.
    /// </remarks>
    internal static async Task<Job?> ClaimJobAsync(
        RefTestManagementContext context,
        Guid jobId,
        int maxAttempts,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lockedUntil = now.Add(lockDuration);

        var claimed = await context.Jobs
            .Where(j => j.Id == jobId
                        && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing)
                        && j.ExecuteAfter <= now
                        && j.Attempts < maxAttempts
                        && (j.LockedUntil == null || j.LockedUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Attempts, j => j.Status == JobStatus.Processing ? j.Attempts + 1 : j.Attempts)
                    .SetProperty(j => j.Status, JobStatus.Processing)
                    .SetProperty(j => j.LockedUntil, lockedUntil)
                    // Bulk updates bypass the change tracker, so ConcurrencyTokenInterceptor never
                    // sees this write. Advance the token here or a tracked reader that loaded the
                    // job before the claim would still be able to save over it.
                    .SetProperty(j => j.Version, j => j.Version + 1),
                cancellationToken);

        if (claimed == 0)
            return null;

        // ExecuteUpdateAsync writes straight to the database without going through the change
        // tracker, so anything this context already tracks for the row still holds the values from
        // before the claim. Querying again would not help: EF resolves a tracked entity from its
        // identity map and keeps the tracked values. Reload is what actually re-reads the row.
        var tracked = context.ChangeTracker.Entries<Job>()
            .FirstOrDefault(entry => entry.Entity.Id == jobId);

        if (tracked is not null)
        {
            await tracked.ReloadAsync(cancellationToken);
            return tracked.Entity;
        }

        return await context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
    }

    /// <summary>
    /// Runs one claimed job and records the outcome.
    /// </summary>
    /// <remarks>
    /// Static and internal for the same reason <see cref="ClaimJobAsync"/> is: this is the failure
    /// policy, and it is worth pinning with tests without standing up the hosted service and its
    /// polling loop. The work itself lives behind <see cref="IJobHandler"/>, so a test supplies a
    /// handler that throws whatever it wants to assert about.
    /// </remarks>
    internal static async Task ProcessJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        RefTestManagementContext context,
        ILogger logger,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        try
        {
            // The job is already locked — ClaimJobAsync took ownership in a single statement
            // before this method was reached.
            ServiceLoggerMessages.LogProcessingJob(logger, job.Id, job.JobType, job.Attempts + 1, maxAttempts);

            // Resolved by key rather than through GetRequiredKeyedService so an unregistered job
            // type still reports itself by name instead of as a DI resolution failure.
            var handler = serviceProvider.GetKeyedService<IJobHandler>(job.JobType)
                          ?? throw new InvalidOperationException($"Unknown job type: {job.JobType}");

            await handler.HandleAsync(job, cancellationToken);

            // Mark as completed
            job.MarkAsCompleted();
            await context.SaveChangesWithRetryAsync(cancellationToken);

            ServiceLoggerMessages.LogJobCompleted(logger, job.Id, job.JobType);
        }
        catch (Exception ex)
        {
            // Mark as failed. The message is persisted to Job.ErrorMessage and that column is not
            // reached by the privacy erasure path, so scrub any address an exception or a
            // third-party API response may have embedded before it is stored or logged.
            //
            // Masking runs a regex with a timeout, so it can throw in its own right. If that were
            // allowed to escape, MarkAsFailed and the save below would never run and the job
            // would be stranded at Processing.
            string errorMessage;
            try
            {
                errorMessage = LogRedaction.MaskEmailsInText(ex.Message) ?? string.Empty;
            }
            catch (Exception maskEx)
            {
                ServiceLoggerMessages.LogErrorMessageMaskingFailed(logger, maskEx, job.Id);
                errorMessage = MaskingFailedMessage;
            }

            if (ex is LegacyReportPayloadException)
            {
                // Legacy report payloads cannot be safely associated with a participant.
                // Quarantine them before they can send data, and clear the payload.
                job.Cancel(errorMessage);
            }
            else if (ex is JobPayloadException)
            {
                // The payload will not parse on a retry either, so stop here instead of holding a
                // slot in the queue for two more passes.
                job.MarkAsPermanentlyFailed(errorMessage);
            }
            else
            {
                job.MarkAsFailed(errorMessage, maxAttempts);
            }

            await context.SaveChangesWithRetryAsync(cancellationToken);

            if (job.Status == JobStatus.Failed)
            {
                ServiceLoggerMessages.LogJobFailedPermanently(logger, job.Id, job.JobType, job.Attempts);
            }
            else
            {
                ServiceLoggerMessages.LogJobFailed(logger, job.Id, job.JobType, job.Attempts, maxAttempts,
                    errorMessage);
            }
        }
    }

    private bool ShouldRunCleanup()
    {
        return _lastCleanupTime == DateTime.MinValue ||
               DateTime.UtcNow - _lastCleanupTime >= _cleanupInterval;
    }

    private async Task CleanupOldJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();

            var now = DateTime.UtcNow;
            var completedCutoff = now - _retainCompletedJobs;
            var failedCutoff = now - _retainFailedJobs;

            ServiceLoggerMessages.LogCleanupStarting(_logger, "Jobs", (int)_retainCompletedJobs.TotalDays);

            // Delete old completed jobs
            var oldJobs = await context.Jobs
                .Where(j =>
                    (j.Status == JobStatus.Completed && j.CompletedAt != null && j.CompletedAt < completedCutoff) ||
                    ((j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled)
                     && j.CompletedAt != null && j.CompletedAt < failedCutoff))
                .ToListAsync(cancellationToken);

            if (oldJobs.Count > 0)
            {
                context.Jobs.RemoveRange(oldJobs);
                await context.SaveChangesWithRetryAsync(cancellationToken);
                ServiceLoggerMessages.LogCleanupCompleted(_logger, "Jobs", oldJobs.Count);
            }
            else
            {
                ServiceLoggerMessages.LogCleanupCompleted(_logger, "Jobs", 0);
            }
        }
        catch (Exception ex)
        {
            ServiceLoggerMessages.LogCleanupError(_logger, ex, "Jobs");
        }
    }
}