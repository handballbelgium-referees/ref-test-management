using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Service for enqueuing background jobs.
/// </summary>
/// <remarks>
/// The <c>Jobs</c> table is an outbox: a job row is the durable record that some side effect still
/// owes to happen. Every enqueue method therefore takes a <c>saveChanges</c> flag.
/// <para>
/// The default (<c>true</c>) writes the job immediately, which is right for callers that are not
/// also writing an entity in the same request. Callers that persist an entity whose existence only
/// makes sense together with its job — creating a RefTest that owes an invitation, approving one
/// that owes a decision email — must pass <c>false</c> and let their own
/// <see cref="RefTestManagementContext.SaveChangesWithRetryAsync"/> commit the entity and the job
/// in a single transaction. Otherwise a failure between the two writes leaves a persisted RefTest
/// whose invitation is never sent.
/// </para>
/// <para>
/// Callers that need the job staged into some other unit of work can pass that
/// <see cref="RefTestManagementContext"/> explicitly; otherwise the service uses its injected
/// scoped context.
/// </para>
/// </remarks>
public interface IJobEnqueueService
{
    Task EnqueueInvitationEmailAsync(InvitationEmailPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task EnqueueReportEmailAsync(ReportEmailPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task EnqueueRefTestExpirationAsync(RefTestExpirationPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task EnqueueApprovalNotificationAsync(ApprovalNotificationEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task EnqueueApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task CancelPendingJobsForRefTestAsync(Guid refTestId,
        bool saveChanges = true, IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task CancelPendingResultEmailsAsync(Guid refTestId,
        bool saveChanges = true, IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);
}

public class JobEnqueueService(RefTestManagementContext context, ILogger<JobEnqueueService> logger)
    : IJobEnqueueService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private IJobPersistenceContext ResolveContext(IJobPersistenceContext? unitOfWorkContext) =>
        unitOfWorkContext ?? context;

    public async Task EnqueueInvitationEmailAsync(InvitationEmailPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.InvitationEmail, payloadJson, executeAfter);

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedInvitationEmail(logger, job.Id, payload.RefTestId);
    }

    public async Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ResultEmail, payloadJson, executeAfter);

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedResultEmail(logger, job.Id, payload.RefTestId);
    }

    public async Task EnqueueReportEmailAsync(ReportEmailPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ReportEmail, payloadJson, executeAfter);

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedReportEmail(logger, job.Id, payload.RecipientEmails.Length);
    }

    public async Task EnqueueRefTestExpirationAsync(RefTestExpirationPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.RefTestExpiration, payloadJson, executeAfter);

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.RefTestExpiration, job.Id);
    }

    public async Task EnqueueApprovalNotificationAsync(
        ApprovalNotificationEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ApprovalNotificationEmail, payloadJson);

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.ApprovalNotificationEmail, job.Id);
    }

    public async Task EnqueueApprovalDecisionEmailAsync(
        ApprovalDecisionEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ApprovalDecisionEmail, payloadJson);

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.ApprovalDecisionEmail, job.Id);
    }

    public async Task CancelPendingJobsForRefTestAsync(Guid refTestId,
        bool saveChanges = true, IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        // Find all pending or processing jobs that could reference this RefTest
        // (InvitationEmail, ResultEmail, RefTestExpiration - but NOT ReportEmail as it's a batch operation)
        var pendingJobs = await dbContext.Jobs
            .Where(j => (j.JobType == JobType.InvitationEmail
                         || j.JobType == JobType.ResultEmail
                         || j.JobType == JobType.RefTestExpiration)
                        && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing))
            .ToListAsync(cancellationToken);

        var canceledCount = 0;
        foreach (var job in pendingJobs)
        {
            // A job cancelled earlier keeps its row but not its payload. There is nothing to
            // match on, and deserializing an empty string throws — which would break this
            // mutation for every other RefTest in the table, not just this one.
            if (string.IsNullOrEmpty(job.Payload))
                continue;

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
            if (saveChanges)
                await dbContext.SaveChangesWithRetryAsync(cancellationToken);
            ServiceLoggerMessages.LogCanceledCountPendingJobsForRefTestRefTestId(logger, canceledCount, refTestId);
        }
    }

    public async Task CancelPendingResultEmailsAsync(Guid refTestId,
        bool saveChanges = true, IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        // Find all pending or processing result email jobs for this RefTest
        var pendingResultJobs = await dbContext.Jobs
            .Where(j => j.JobType == JobType.ResultEmail
                        && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing))
            .ToListAsync(cancellationToken);

        var canceledCount = 0;
        foreach (var job in from job in pendingResultJobs
                 where !string.IsNullOrEmpty(job.Payload)
                 let payload = JsonSerializer.Deserialize<ResultEmailPayload>(job.Payload, _jsonOptions)
                 where payload?.RefTestId == refTestId
                 select job)
        {
            job.Cancel($"RefTest {refTestId} was soft reset (results cleared)");
            canceledCount++;
        }

        if (canceledCount > 0)
        {
            if (saveChanges)
                await dbContext.SaveChangesWithRetryAsync(cancellationToken);
            ServiceLoggerMessages.LogCanceledCountPendingResultEmailJobsForRefTestRefTestId(logger, canceledCount, refTestId);
        }
    }
}