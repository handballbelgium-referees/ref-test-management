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
                ServiceLoggerMessages.LogServiceError(_logger, ex, nameof(BackgroundJobService));
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
        var now = DateTime.UtcNow;
        var jobs = await context.Jobs
            .Where(j => j.Status == JobStatus.Pending
                        && j.ExecuteAfter <= now
                        && (j.LockedUntil == null || j.LockedUntil <= now))
            .OrderBy(j => j.ExecuteAfter)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            ServiceLoggerMessages.LogNoJobsAvailable(_logger);
            return;
        }

        ServiceLoggerMessages.LogJobsFound(_logger, jobs.Count);

        // Process only jobs that are ready (defensive check using domain method)
        foreach (var job in jobs.Where(j => j.IsReadyToProcess())
                     .TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            await ProcessJobAsync(job, scope.ServiceProvider, context, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(
        Job job,
        IServiceProvider serviceProvider,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Lock the job
            job.MarkAsProcessing(_lockDuration);
            await context.SaveChangesAsync(cancellationToken);

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
            await context.SaveChangesAsync(cancellationToken);

            ServiceLoggerMessages.LogJobCompleted(_logger, job.Id, job.JobType);
        }
        catch (Exception ex)
        {
            // Mark as failed
            var errorMessage = ex.Message;
            job.MarkAsFailed(errorMessage, _maxAttempts);
            await context.SaveChangesAsync(cancellationToken);

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

        ServiceLoggerMessages.LogSendingInvitationEmail(_logger, payload.Email);

        await emailService.SendRefTestInvitationAsync(
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
            await context.SaveChangesAsync(cancellationToken);

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

        ServiceLoggerMessages.LogSendingResultEmail(_logger, payload.Email);

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
        await context.SaveChangesAsync(cancellationToken);

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

        // Skip if already completed or expired
        if (refTest.Status == RefTestStatus.Completed || refTest.Status == RefTestStatus.Expired)
        {
            _logger.LogDebug("RefTest {Id} already in status {Status}, skipping", refTest.Id, refTest.Status);
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

                    await RefTestLifecycleMutations.CompleteRefTestAsync(
                        new CompleteRefTestInput(refTest.Token, refTest.SelectedAnswerIds, refTest.Language),
                        context,
                        ihfRulesQuestionsService,
                        jobEnqueueService,
                        emailConfiguration,
                        subscriptionService,
                        cancellationToken);

                    ServiceLoggerMessages.LogAutoCompleted(_logger, refTest.Id, refTest.Email);
                    break;
                }
                case RefTestExpirationAction.MarkAsExpired:
                    // Mark as expired (for pending tests)
                    refTest.Expire();
                    await context.SaveChangesAsync(cancellationToken);

                    // Publish subscription event
                    await subscriptionService.PublishRefTestExpiredAsync(
                        refTest.Id,
                        refTest.Status,
                        DateTime.UtcNow,
                        cancellationToken);

                    ServiceLoggerMessages.LogExpired(_logger, refTest.Id, refTest.Status, refTest.Email);
                    break;
            }
        }
        catch (Exception ex)
        {
            ServiceLoggerMessages.LogAutoCompleteFailed(_logger, ex, refTest.Id, refTest.Email);
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
            .Select(rt => ($"{rt.FirstName} {rt.LastName}", rt.Email))
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
            .Select(rt => ($"{rt.FirstName} {rt.LastName}", rt.Email))
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
            payload.CreatorEmail,
            payload.RefTests.Count);
    }

    private T DeserializePayload<T>(Job job) where T : IJobPayload
    {
        try
        {
            var payload = JsonSerializer.Deserialize<T>(job.Payload, _jsonSerializerOptions);

            return payload ?? throw new InvalidOperationException("Deserialized payload is null");
        }
        catch (Exception ex)
        {
            ServiceLoggerMessages.LogJobDeserializationError(_logger, ex, job.Id);
            throw;
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
                    (j.Status == JobStatus.Failed && j.CompletedAt != null && j.CompletedAt < failedCutoff))
                .ToListAsync(cancellationToken);

            if (oldJobs.Count > 0)
            {
                context.Jobs.RemoveRange(oldJobs);
                await context.SaveChangesAsync(cancellationToken);
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