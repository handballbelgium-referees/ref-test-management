using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Service for enqueuing background jobs
/// </summary>
public interface IJobEnqueueService
{
    Task EnqueueInvitationEmailAsync(InvitationEmailPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default);

    Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default);

    Task EnqueueReportEmailAsync(ReportEmailPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default);

    Task EnqueueRefTestExpirationAsync(RefTestExpirationPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default);

    Task EnqueueApprovalNotificationAsync(ApprovalNotificationEmailPayload payload,
        CancellationToken cancellationToken = default);

    Task EnqueueApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload,
        CancellationToken cancellationToken = default);

    Task CancelPendingJobsForRefTestAsync(Guid refTestId, CancellationToken cancellationToken = default);
    Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken = default);
}

public class JobEnqueueService(RefTestManagementContext context, ILogger<JobEnqueueService> logger)
    : IJobEnqueueService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task EnqueueInvitationEmailAsync(InvitationEmailPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.InvitationEmail, payloadJson, executeAfter);

        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedInvitationEmail(logger, job.Id, payload.Email);
    }

    public async Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ResultEmail, payloadJson, executeAfter);

        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedResultEmail(logger, job.Id, payload.Email);
    }

    public async Task EnqueueReportEmailAsync(ReportEmailPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ReportEmail, payloadJson, executeAfter);

        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedReportEmail(logger, job.Id, payload.RecipientEmails.Length);
    }

    public async Task EnqueueRefTestExpirationAsync(RefTestExpirationPayload payload, DateTime? executeAfter = null,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.RefTestExpiration, payloadJson, executeAfter);

        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.RefTestExpiration, job.Id);
    }

    public async Task EnqueueApprovalNotificationAsync(
        ApprovalNotificationEmailPayload payload,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ApprovalNotificationEmail, payloadJson);

        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.ApprovalNotificationEmail, job.Id);
    }

    public async Task EnqueueApprovalDecisionEmailAsync(
        ApprovalDecisionEmailPayload payload,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ApprovalDecisionEmail, payloadJson);

        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.ApprovalDecisionEmail, job.Id);
    }

    public async Task CancelPendingJobsForRefTestAsync(Guid refTestId, CancellationToken cancellationToken = default)
    {
        // Find all pending or processing jobs that could reference this RefTest
        // (InvitationEmail, ResultEmail, RefTestExpiration - but NOT ReportEmail as it's a batch operation)
        var pendingJobs = await context.Jobs
            .Where(j => (j.JobType == JobType.InvitationEmail
                         || j.JobType == JobType.ResultEmail
                         || j.JobType == JobType.RefTestExpiration)
                        && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing))
            .ToListAsync(cancellationToken);

        var canceledCount = 0;
        foreach (var job in pendingJobs)
        {
            bool shouldCancel;

            // Check if this job is for the specific RefTest based on job type
            switch (job.JobType)
            {
                case JobType.InvitationEmail:
                    var invitationPayload =
                        JsonSerializer.Deserialize<InvitationEmailPayload>(job.Payload, _jsonOptions);
                    shouldCancel = invitationPayload?.RefTestId == refTestId;
                    break;

                case JobType.ResultEmail:
                    var resultPayload = JsonSerializer.Deserialize<ResultEmailPayload>(job.Payload, _jsonOptions);
                    shouldCancel = resultPayload?.RefTestId == refTestId;
                    break;

                case JobType.RefTestExpiration:
                    var expirationPayload =
                        JsonSerializer.Deserialize<RefTestExpirationPayload>(job.Payload, _jsonOptions);
                    shouldCancel = expirationPayload?.RefTestId == refTestId;
                    break;
                case JobType.ReportEmail:
                default:
                    shouldCancel = false;
                    break;
            }

            if (!shouldCancel)
                continue;

            job.Cancel($"RefTest {refTestId} was reset/revived");
            canceledCount++;
        }

        if (canceledCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            ServiceLoggerMessages.LogCanceledCountPendingJobsForRefTestRefTestId(logger, canceledCount, refTestId);
        }
    }

    public async Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken = default)
    {
        // Find all pending or processing result email jobs for this RefTest
        var pendingResultJobs = await context.Jobs
            .Where(j => j.JobType == JobType.ResultEmail
                        && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing))
            .ToListAsync(cancellationToken);

        var canceledCount = 0;
        foreach (var job in from job in pendingResultJobs
                 let payload = JsonSerializer.Deserialize<ResultEmailPayload>(job.Payload, _jsonOptions)
                 where payload?.RefTestId == refTestId
                 select job)
        {
            job.Cancel($"RefTest {refTestId} was soft reset (results cleared)");
            canceledCount++;
        }

        if (canceledCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            ServiceLoggerMessages.LogCanceledCountPendingResultEmailJobsForRefTestRefTestId(logger, canceledCount, refTestId);
        }
    }
}