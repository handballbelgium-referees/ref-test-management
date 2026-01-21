using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Background service that processes jobs from a database queue
/// </summary>
public partial class BackgroundJobService : BackgroundService
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

    // High-performance logging using source generators
    [LoggerMessage(Level = LogLevel.Information, Message = "BackgroundJobService is starting")]
    partial void LogServiceStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "BackgroundJobService is stopping")]
    partial void LogServiceStopping();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while processing jobs")]
    partial void LogProcessingError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No jobs available for processing")]
    partial void LogNoJobsAvailable();

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {count} jobs to process")]
    partial void LogJobsFound(int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processing job {jobId} of type {jobType} (attempt {attempt}/{maxAttempts})")]
    partial void LogProcessingJob(Guid jobId, JobType jobType, int attempt, int maxAttempts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully completed job {jobId} of type {jobType}")]
    partial void LogJobCompleted(Guid jobId, JobType jobType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Job {jobId} of type {jobType} failed (attempt {attempt}/{maxAttempts}): {errorMessage}")]
    partial void LogJobFailed(Guid jobId, JobType jobType, int attempt, int maxAttempts, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Job {jobId} of type {jobType} failed permanently after {attempts} attempts")]
    partial void LogJobFailedPermanently(Guid jobId, JobType jobType, int attempts);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending invitation email to {email}")]
    partial void LogSendingInvitationEmail(string email);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending result email to {email}")]
    partial void LogSendingResultEmail(string email);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending report email to {count} recipients")]
    partial void LogSendingReportEmail(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize job payload for job {jobId}")]
    partial void LogDeserializationError(Exception ex, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Running job cleanup - removing jobs older than: Completed={completedDays} days, Failed={failedDays} days")]
    partial void LogCleanupStarting(int completedDays, int failedDays);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job cleanup completed - removed {count} old jobs")]
    partial void LogCleanupCompleted(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred during job cleanup")]
    partial void LogCleanupError(Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarting();

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
                LogProcessingError(ex);
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when the service is stopping
                break;
            }
        }

        LogServiceStopping();
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
            LogNoJobsAvailable();
            return;
        }

        LogJobsFound(jobs.Count);

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

            LogProcessingJob(job.Id, job.JobType, job.Attempts + 1, _maxAttempts);

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

            LogJobCompleted(job.Id, job.JobType);
        }
        catch (Exception ex)
        {
            // Mark as failed
            var errorMessage = ex.Message;
            job.MarkAsFailed(errorMessage, _maxAttempts);
            await context.SaveChangesAsync(cancellationToken);

            if (job.Status == JobStatus.Failed)
            {
                LogJobFailedPermanently(job.Id, job.JobType, job.Attempts);
            }
            else
            {
                LogJobFailed(job.Id, job.JobType, job.Attempts, _maxAttempts, errorMessage);
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

        LogSendingInvitationEmail(payload.Email);

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

        LogSendingResultEmail(payload.Email);

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

        LogSendingReportEmail(payload.RecipientEmails.Length);

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
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var payload = JsonSerializer.Deserialize<T>(job.Payload, options);

            return payload ?? throw new InvalidOperationException("Deserialized payload is null");
        }
        catch (Exception ex)
        {
            LogDeserializationError(ex, job.Id);
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

            LogCleanupStarting((int)_retainCompletedJobs.TotalDays, (int)_retainFailedJobs.TotalDays);

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
                LogCleanupCompleted(oldJobs.Count);
            }
            else
            {
                LogCleanupCompleted(0);
            }
        }
        catch (Exception ex)
        {
            LogCleanupError(ex);
        }
    }
}