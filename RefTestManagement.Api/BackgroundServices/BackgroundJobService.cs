using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
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

        foreach (var job in jobs.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
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
                ServiceLoggerMessages.LogJobFailed(_logger, job.Id, job.JobType, job.Attempts, _maxAttempts, errorMessage);
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

        ServiceLoggerMessages.LogSendingResultEmail(_logger, payload.Email);

        // Get questions with correct answers
        var questionsWithCorrectAnswers = await questionsService.GetQuestionsByIdAsync(
            payload.WrongQuestionIds.Concat(payload.WrongAnswerIds).Distinct().ToList(),
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
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(r => r.Id == payload.RefTestId, cancellationToken);
        
        if (refTest != null)
        {
            refTest.SendResults();
            await context.SaveChangesAsync(cancellationToken);
        }
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