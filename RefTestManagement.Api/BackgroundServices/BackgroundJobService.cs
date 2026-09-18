using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Auth0;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
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

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

            await ProcessJobAsync(job, scope.ServiceProvider, context, cancellationToken);
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

    private async Task ProcessJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // The job is already locked — ClaimJobAsync took ownership in a single statement
            // before this method was reached.
            ServiceLoggerMessages.LogProcessingJob(_logger, job.Id, job.JobType, job.Attempts + 1, _maxAttempts);

            // Process based on a job type
            switch (job.JobType)
            {
                case JobType.InvitationEmail:
                    await ProcessInvitationEmailJobAsync(job, serviceProvider, cancellationToken);
                    break;

                case JobType.ResultEmail:
                    await ProcessResultEmailJobAsync(job, serviceProvider, cancellationToken);
                    break;

                case JobType.ReportEmail:
                    await ProcessReportEmailJobAsync(job, serviceProvider, cancellationToken);
                    break;

                case JobType.RefTestExpiration:
                    await ProcessRefTestExpirationJobAsync(job, serviceProvider, cancellationToken);
                    break;

                case JobType.ApprovalNotificationEmail:
                    await ProcessApprovalNotificationEmailJobAsync(job, serviceProvider, cancellationToken);
                    break;

                case JobType.ApprovalDecisionEmail:
                    await ProcessApprovalDecisionEmailJobAsync(job, serviceProvider, cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown job type: {job.JobType}");
            }

            // Mark as completed
            job.MarkAsCompleted();
            await context.SaveChangesWithRetryAsync(cancellationToken);

            ServiceLoggerMessages.LogJobCompleted(_logger, job.Id, job.JobType);
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
                ServiceLoggerMessages.LogErrorMessageMaskingFailed(_logger, maskEx, job.Id);
                errorMessage = MaskingFailedMessage;
            }

            if (ex is JobPayloadException)
            {
                // The payload will not parse on a retry either, so stop here instead of holding a
                // slot in the queue for two more passes.
                job.MarkAsPermanentlyFailed(errorMessage);
            }
            else
            {
                job.MarkAsFailed(errorMessage, _maxAttempts);
            }

            await context.SaveChangesWithRetryAsync(cancellationToken);

            if (job.Status == JobStatus.Failed)
            {
                ServiceLoggerMessages.LogJobFailedPermanently(_logger, job.Id, job.JobType, job.Attempts);
            }
            else
            {
                ServiceLoggerMessages.LogJobFailed(_logger, job.Id, job.JobType, job.Attempts, _maxAttempts,
                    errorMessage);
            }
        }
    }

    private async Task ProcessInvitationEmailJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<InvitationEmailPayload>(job);
        var emailService = serviceProvider.GetRequiredService<IEmailService>();
        var context = serviceProvider.GetRequiredService<RefTestManagementContext>();
        var subscriptionService = serviceProvider.GetRequiredService<IRefTestSubscriptionService>();

        ServiceLoggerMessages.LogSendingInvitationEmail(_logger, payload.RefTestId);

        await emailService.SendRefTestInvitationAsync(
            payload.RefTestId,
            payload.Name,
            payload.Email,
            payload.Token,
            payload.NumberOfQuestions,
            payload.MaxTimeInMinutes,
            cancellationToken);

        // Mark the RefTest invitation as sent
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(r => r.Id == payload.RefTestId, cancellationToken);

        if (refTest != null)
        {
            refTest.SendInvitation();
            await context.SaveChangesWithRetryAsync(cancellationToken);

            // Publish subscription event
            await subscriptionService.PublishInvitationSentAsync(
                refTest.Id,
                refTest.InvitationSentAt!.Value,
                cancellationToken);
        }
    }

    private async Task ProcessResultEmailJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<ResultEmailPayload>(job);
        var emailService = serviceProvider.GetRequiredService<IEmailService>();
        var questionsService = serviceProvider.GetRequiredService<IIhfRulesQuestionsService>();
        var context = serviceProvider.GetRequiredService<RefTestManagementContext>();
        var subscriptionService = serviceProvider.GetRequiredService<IRefTestSubscriptionService>();

        ServiceLoggerMessages.LogSendingResultEmail(_logger, payload.RefTestId);

        // Get the RefTest to retrieve all question IDs
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(r => r.Id == payload.RefTestId, cancellationToken);

        if (refTest == null)
        {
            throw new InvalidOperationException($"RefTest {payload.RefTestId} not found");
        }

        // Get ALL questions with correct answers (not just the wrong ones)
        var questionsWithCorrectAnswers = await questionsService.GetQuestionsByIdAsync(
            refTest.QuestionIds,
            includeNumber: true,
            includeIsCorrect: true,
            randomAnswerOrder: false,
            cancellationToken: cancellationToken);

        await emailService.SendRefTestResultsAsync(
            payload.RefTestId,
            payload.Name,
            payload.Email,
            payload.QuestionScore,
            payload.AnswerScore,
            payload.TotalQuestions,
            payload.AnswerTotal,
            payload.Percentage,
            payload.SelectedAnswerIds,
            payload.WrongQuestionIds,
            payload.WrongAnswerIds,
            questionsWithCorrectAnswers,
            scheduleEmail: false,
            cancellationToken); // Already scheduled via the job system

        // Mark the RefTest results as sent
        refTest.SendResults();
        await context.SaveChangesWithRetryAsync(cancellationToken);

        // Publish subscription event
        await subscriptionService.PublishResultSentAsync(
            refTest.Id,
            refTest.ResultsSentAt!.Value,
            cancellationToken);
    }

    private async Task ProcessReportEmailJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<ReportEmailPayload>(job);
        var reportService = serviceProvider.GetRequiredService<IRefTestReportService>();

        ServiceLoggerMessages.LogSendingReportEmail(_logger, payload.RecipientEmails.Length);

        // Convert payload data to service data
        var refTests = payload.RefTests.Select(r => new RefTestReportData(
            r.TitleName,
            r.FirstName,
            r.LastName,
            r.StartedAt,
            r.CompletedAt,
            r.QuestionScore,
            r.QuestionTotal,
            r.AnswerScore,
            r.AnswerTotal,
            r.Percentage,
            r.Passed,
            r.Language,
            r.Duration)).ToList();

        await reportService.SendReportAsync(refTests, payload.RecipientEmails, cancellationToken);
    }

    private async Task ProcessRefTestExpirationJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<RefTestExpirationPayload>(job);

        var contextFactory = serviceProvider.GetRequiredService<IDbContextFactory<RefTestManagementContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var subscriptionService = serviceProvider.GetRequiredService<IRefTestSubscriptionService>();

        // Load the specific RefTest
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == payload.RefTestId, cancellationToken);

        if (refTest == null)
        {
            _logger.LogWarning("RefTest {Id} not found for expiration job", payload.RefTestId);
            return;
        }

        // Skip if already completed, expired, or its consent has been withdrawn
        if (refTest.Status == RefTestStatus.Completed || refTest.Status == RefTestStatus.Expired ||
            refTest.IsAnonymized)
        {
            _logger.LogDebug(
                "RefTest {Id} already in status {Status} or anonymized ({IsAnonymized}), skipping",
                refTest.Id, refTest.Status, refTest.IsAnonymized);
            return;
        }

        ServiceLoggerMessages.LogRefTestExpirationCheck(_logger, refTest.Id, true, refTest.Status);

        try
        {
            switch (payload.Action)
            {
                case RefTestExpirationAction.AutoComplete when refTest.Status == RefTestStatus.InProgress:
                {
                    // Auto-complete the in-progress test
                    var ihfRulesQuestionsService = serviceProvider.GetRequiredService<IIhfRulesQuestionsService>();
                    var jobEnqueueService = serviceProvider.GetRequiredService<IJobEnqueueService>();
                    var emailConfiguration = serviceProvider.GetRequiredService<EmailConfiguration>();

                    await RefTestLifecycleMutations.CompleteRefTestCoreAsync(
                        new CompleteRefTestInput(refTest.Token, refTest.SelectedAnswerIds, refTest.Language),
                        context,
                        ihfRulesQuestionsService,
                        jobEnqueueService,
                        emailConfiguration,
                        subscriptionService,
                        RefTestCompletionSource.ExpirationService,
                        cancellationToken);

                    ServiceLoggerMessages.LogAutoCompleted(_logger, refTest.Id);
                    break;
                }
                case RefTestExpirationAction.MarkAsExpired:
                    // Mark as expired (for pending tests)
                    refTest.Expire();
                    await context.SaveChangesWithRetryAsync(cancellationToken);

                    // Publish subscription event
                    await subscriptionService.PublishRefTestExpiredAsync(
                        refTest.Id,
                        refTest.Status,
                        DateTime.UtcNow,
                        cancellationToken);

                    ServiceLoggerMessages.LogExpired(_logger, refTest.Id, refTest.Status);
                    break;
            }
        }
        catch (Exception ex)
        {
            ServiceLoggerMessages.LogAutoCompleteFailed(_logger, ex, refTest.Id);
            throw; // Re-throw so the job can be retried
        }
    }

    private async Task ProcessApprovalNotificationEmailJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<ApprovalNotificationEmailPayload>(job);
        var auth0Service = serviceProvider.GetRequiredService<IAuth0ManagementService>();
        var emailService = serviceProvider.GetRequiredService<IEmailService>();
        var emailConfig = serviceProvider.GetRequiredService<EmailConfiguration>();

        var approvers = await auth0Service.GetUsersWithPermissionAsync(
            Permissions.RefTests.Approve, cancellationToken);

        if (approvers.Count == 0)
        {
            _logger.LogWarning(
                "No approvers found for permission '{Permission}' — approval notification email not sent",
                Permissions.RefTests.Approve);
            return;
        }

        var items = payload.RefTests
            .Select(rt => ($"{rt.FirstName} {rt.LastName}", rt.Email, rt.ScheduledAt))
            .ToList();

        foreach (var approver in approvers)
        {
            await emailService.SendApprovalNotificationAsync(
                approver.Name,
                approver.Email,
                payload.CreatorName,
                payload.TitleValue,
                items,
                emailConfig.BaseUrl,
                cancellationToken);
        }

        ServiceLoggerMessages.LogApprovalNotificationSent(_logger, approvers.Count, payload.RefTests.Count);
    }

    private async Task ProcessApprovalDecisionEmailJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<ApprovalDecisionEmailPayload>(job);
        var emailService = serviceProvider.GetRequiredService<IEmailService>();

        var items = payload.RefTests
            .Select(rt => ($"{rt.FirstName} {rt.LastName}", rt.Email, rt.ScheduledAt))
            .ToList();

        await emailService.SendApprovalDecisionAsync(
            payload.CreatorName,
            payload.CreatorEmail,
            payload.ApproverName,
            payload.IsApproved,
            payload.RejectionReason,
            payload.TitleValue,
            items,
            cancellationToken);

        ServiceLoggerMessages.LogApprovalDecisionEmailSent(
            _logger,
            payload.IsApproved ? "approved" : "rejected",
            LogRedaction.MaskEmail(payload.CreatorEmail),
            payload.RefTests.Count);
    }

    private T DeserializePayload<T>(Job job) where T : IJobPayload
    {
        // A cancelled job has its payload cleared, and a job that was cancelled while a worker
        // held it can still reach a handler. Treat that as terminal rather than letting
        // JsonException burn every retry attempt.
        if (string.IsNullOrEmpty(job.Payload))
            throw new JobPayloadException($"Job {job.Id} has no payload to deserialize");

        try
        {
            var payload = JsonSerializer.Deserialize<T>(job.Payload, _jsonSerializerOptions);

            return payload ?? throw new JobPayloadException($"Job {job.Id} deserialized to a null payload");
        }
        catch (JsonException ex)
        {
            // The payload carries the participant's name and email; a deserialization failure
            // can quote the offending fragment back in its message.
            ServiceLoggerMessages.LogJobDeserializationError(_logger, LogRedaction.MaskEmails(ex), job.Id);
            throw new JobPayloadException($"Job {job.Id} has a payload that could not be deserialized", ex);
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