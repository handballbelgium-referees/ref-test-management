using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Background service that processes jobs from a database queue
/// </summary>
public class BackgroundJobService : PollingBackgroundService
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

    protected override TimeSpan StartupDelay => _startupDelay;

    protected override TimeSpan Interval => _pollingInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceLoggerMessages.LogServiceStarting(_logger, nameof(BackgroundJobService));
        await base.ExecuteAsync(stoppingToken);
        ServiceLoggerMessages.LogServiceStopping(_logger, nameof(BackgroundJobService));
    }

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await ProcessJobsAsync(cancellationToken);

        if (_enableCleanup && ShouldRunCleanup())
        {
            await CleanupOldJobsAsync(cancellationToken);
            _lastCleanupTime = DateTime.UtcNow;
        }
    }

    // Anything raised while processing jobs can have travelled through a payload or an email
    // provider response, so mask before the exception reaches a log sink.
    protected override void LogFailure(Exception exception) =>
        ServiceLoggerMessages.LogServiceError(_logger, LogRedaction.MaskEmails(exception), nameof(BackgroundJobService));

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var jobQueueStore = scope.ServiceProvider.GetRequiredService<IJobQueueStore>();
        var exportRequestCleanup =
            scope.ServiceProvider.GetRequiredService<IPersonalDataExportRequestCleanup>();

        await FailExpiredFinalAttemptJobsAsync(
            jobQueueStore,
            exportRequestCleanup,
            _logger,
            _maxAttempts,
            _batchSize,
            cancellationToken);

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
        var candidateIds = await jobQueueStore.GetReadyJobIdsAsync(
            now,
            _maxAttempts,
            _batchSize,
            cancellationToken);

        if (candidateIds.Count == 0)
        {
            ServiceLoggerMessages.LogNoJobsAvailable(_logger);
            return;
        }

        ServiceLoggerMessages.LogJobsFound(_logger, candidateIds.Count);

        foreach (var candidateId in candidateIds.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            var job = await jobQueueStore.ClaimJobAsync(
                candidateId,
                _maxAttempts,
                _lockDuration,
                cancellationToken);

            // Lost the race — another instance holds the lock now.
            if (job is null)
                continue;

            await ProcessJobAsync(
                job,
                scope.ServiceProvider,
                jobQueueStore,
                _logger,
                _maxAttempts,
                cancellationToken);
        }
    }

    internal static async Task<int> FailExpiredFinalAttemptJobsAsync(
        IJobQueueStore jobQueueStore,
        IPersonalDataExportRequestCleanup exportRequestCleanup,
        ILogger logger,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var failedJobs = await jobQueueStore.FailExpiredFinalAttemptJobsAsync(
            DateTime.UtcNow,
            maxAttempts,
            batchSize,
            cancellationToken);
        foreach (var failedJob in failedJobs)
        {
            ServiceLoggerMessages.LogJobFailedPermanently(
                logger,
                failedJob.JobId,
                failedJob.JobType,
                failedJob.Attempts);

            if (failedJob.JobType != JobType.PersonalDataExportDeliveryEmail)
                continue;

            try
            {
                await exportRequestCleanup.ClearExpiredChallengesAsync(
                    DateTime.UtcNow,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // The failed job is already durable. The duplicate-aware cleanup service retries
                // clearing a verified request after active delivery work ends.
                logger.LogError(
                    "Terminal export-request cleanup failed for background job {JobId}",
                    failedJob.JobId);
            }
        }

        return failedJobs.Count;
    }

    /// <summary>
    /// Runs one claimed job and records the outcome.
    /// </summary>
    /// <remarks>
    /// Static and internal so this failure policy can be tested without standing up the hosted
    /// service and polling loop. The work itself lives behind <see cref="IJobHandler"/>, so a test
    /// supplies a handler that throws whatever it wants to assert about.
    /// </remarks>
    internal static async Task ProcessJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        IJobQueueStore jobQueueStore,
        ILogger logger,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var clock = serviceProvider.GetService<TimeProvider>();

        try
        {
            // The job is already locked — <see cref="IJobQueueStore.ClaimJobAsync"/> took ownership in one statement
            // before this method was reached.
            ServiceLoggerMessages.LogProcessingJob(logger, job.Id, job.JobType, job.Attempts + 1, maxAttempts);

            // Resolved by key rather than through GetRequiredKeyedService so an unregistered job
            // type still reports itself by name instead of as a DI resolution failure.
            var handler = serviceProvider.GetKeyedService<IJobHandler>(job.JobType)
                          ?? throw new InvalidOperationException($"Unknown job type: {job.JobType}");

            // Jobs run one at a time on this scope, so the current job id is unambiguous.
            var jobExecution = serviceProvider.GetService<JobExecutionContext>();
            if (jobExecution is not null)
                jobExecution.CurrentJobId = job.Id;
            try
            {
                await handler.HandleAsync(job, cancellationToken);
            }
            finally
            {
                if (jobExecution is not null)
                    jobExecution.CurrentJobId = null;
            }

            // Mark as completed
            job.MarkAsCompleted(clock?.GetUtcNow().UtcDateTime);
            await jobQueueStore.SaveChangesAsync(cancellationToken);

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
                job.Cancel(errorMessage, clock?.GetUtcNow().UtcDateTime);
            }
            else if (ex is JobPayloadException)
            {
                // The payload will not parse on a retry either, so stop here instead of holding a
                // slot in the queue for two more passes.
                job.MarkAsPermanentlyFailed(errorMessage, clock?.GetUtcNow().UtcDateTime);
            }
            else
            {
                job.MarkAsFailed(errorMessage, maxAttempts, clock?.GetUtcNow().UtcDateTime);
            }

            await jobQueueStore.SaveChangesAsync(cancellationToken);

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
            var jobQueueStore = scope.ServiceProvider.GetRequiredService<IJobQueueStore>();

            var now = DateTime.UtcNow;
            var completedCutoff = now - _retainCompletedJobs;
            var failedCutoff = now - _retainFailedJobs;

            ServiceLoggerMessages.LogCleanupStarting(_logger, "Jobs", (int)_retainCompletedJobs.TotalDays);

            var deletedCount = await jobQueueStore.DeleteOldJobsAsync(
                completedCutoff,
                failedCutoff,
                cancellationToken);
            ServiceLoggerMessages.LogCleanupCompleted(_logger, "Jobs", deletedCount);
        }
        catch (Exception ex)
        {
            ServiceLoggerMessages.LogCleanupError(_logger, ex, "Jobs");
        }
    }
}