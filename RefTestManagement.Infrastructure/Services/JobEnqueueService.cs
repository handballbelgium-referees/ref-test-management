using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
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
    Task EnqueueInvitationEmailAsync(RefTest refTest, DateTime? executeAfter = null,
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

    Task EnqueuePersonalDataExportChallengeEmailAsync(PersonalDataExportChallengeEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task EnqueuePersonalDataExportDeliveryEmailAsync(PersonalDataExportDeliveryEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task EnqueuePrivacyWithdrawalChallengeEmailAsync(PrivacyWithdrawalChallengeEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default);

    Task<Guid> EnqueuePrivacyWithdrawalBatchAsync(PrivacyWithdrawalBatchPayload payload,
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

public class JobEnqueueService(
    RefTestManagementContext context,
    IRefTestInvitationTokenProtection tokenProtection,
    ILogger<JobEnqueueService> logger,
    TimeProvider? timeProvider = null)
    : IJobEnqueueService
{
    private DateTime? Now() => timeProvider?.GetUtcNow().UtcDateTime;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private IJobPersistenceContext ResolveContext(IJobPersistenceContext? unitOfWorkContext) =>
        unitOfWorkContext ?? context;

    public async Task EnqueueInvitationEmailAsync(RefTest refTest, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var token = refTest.GetIssuedToken();
        var protectedToken = tokenProtection.Protect(token);
        var payload = new InvitationEmailPayload(
            refTest.Id,
            refTest.FullName,
            refTest.Email,
            refTest.Token,
            refTest.NumberOfQuestions,
            refTest.MaxTimeInMinutes);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.InvitationEmail, payloadJson, executeAfter, now: Now(), refTestId: refTest.Id);

        // Prepare the job completely before mutating the RefTest, so a payload/protection failure
        // cannot leave a token update staged without the corresponding outbox row.
        refTest.StoreProtectedInvitationToken(protectedToken);
        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedInvitationEmail(logger, job.Id, refTest.Id);
    }

    public async Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ResultEmail, payloadJson, executeAfter, now: Now(), refTestId: payload.RefTestId);

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
        var job = Job.Create(JobType.ReportEmail, payloadJson, executeAfter, now: Now());

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
        var job = Job.Create(JobType.RefTestExpiration, payloadJson, executeAfter, now: Now(), refTestId: payload.RefTestId);

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
        var job = Job.Create(JobType.ApprovalNotificationEmail, payloadJson, now: Now());

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
        var job = Job.Create(JobType.ApprovalDecisionEmail, payloadJson, now: Now());

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.ApprovalDecisionEmail, job.Id);
    }

    public async Task EnqueuePersonalDataExportChallengeEmailAsync(
        PersonalDataExportChallengeEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.PersonalDataExportChallengeEmail, payloadJson, now: Now());

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.PersonalDataExportChallengeEmail, job.Id);
    }

    public async Task EnqueuePersonalDataExportDeliveryEmailAsync(
        PersonalDataExportDeliveryEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.PersonalDataExportDeliveryEmail, payloadJson, now: Now());

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.PersonalDataExportDeliveryEmail, job.Id);
    }

    public async Task EnqueuePrivacyWithdrawalChallengeEmailAsync(
        PrivacyWithdrawalChallengeEmailPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.PrivacyWithdrawalChallengeEmail, payloadJson, now: Now());

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.PrivacyWithdrawalChallengeEmail, job.Id);
    }

    public async Task<Guid> EnqueuePrivacyWithdrawalBatchAsync(
        PrivacyWithdrawalBatchPayload payload,
        bool saveChanges = true,
        IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(
            JobType.PrivacyWithdrawalBatch,
            payloadJson,
            privacyWithdrawalBatchId: payload.BatchId,
            now: Now());

        dbContext.Jobs.Add(job);
        if (saveChanges)
            await dbContext.SaveChangesWithRetryAsync(cancellationToken);

        ServiceLoggerMessages.LogJobEnqueued(logger, JobType.PrivacyWithdrawalBatch, job.Id);
        return job.Id;
    }

    public async Task CancelPendingJobsForRefTestAsync(Guid refTestId,
        bool saveChanges = true, IJobPersistenceContext? unitOfWorkContext = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = ResolveContext(unitOfWorkContext);
        // Per-RefTest jobs (InvitationEmail, ResultEmail, RefTestExpiration — not the batch
        // ReportEmail) carry their RefTest in an indexed column. Jobs queued before that column
        // existed have it null and are matched on their payload instead.
        // ponytail: legacy-payload fallback; drop once no pending job with a null RefTestId remains.
        var pendingJobs = await dbContext.Jobs
            .Where(j => (j.JobType == JobType.InvitationEmail
                         || j.JobType == JobType.ResultEmail
                         || j.JobType == JobType.RefTestExpiration)
                        && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing)
                        && (j.RefTestId == refTestId || j.RefTestId == null))
            .ToListAsync(cancellationToken);

        var canceledCount = 0;
        foreach (var job in pendingJobs)
        {
            if (job.RefTestId is null && !LegacyPayloadReferences(job, refTestId))
                continue;

            job.Cancel($"RefTest {refTestId} was reset/revived", Now());
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
        var pendingResultJobs = await dbContext.Jobs
            .Where(j => j.JobType == JobType.ResultEmail
                        && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing)
                        && (j.RefTestId == refTestId || j.RefTestId == null))
            .ToListAsync(cancellationToken);

        var canceledCount = 0;
        foreach (var job in pendingResultJobs.Where(job => job.RefTestId is not null || LegacyPayloadReferences(job, refTestId)))
        {
            job.Cancel($"RefTest {refTestId} was soft reset (results cleared)", Now());
            canceledCount++;
        }

        if (canceledCount > 0)
        {
            if (saveChanges)
                await dbContext.SaveChangesWithRetryAsync(cancellationToken);
            ServiceLoggerMessages.LogCanceledCountPendingResultEmailJobsForRefTestRefTestId(logger, canceledCount, refTestId);
        }
    }

    /// <summary>Matches a job queued before <see cref="Job.RefTestId"/> existed on its payload.</summary>
    private bool LegacyPayloadReferences(Job job, Guid refTestId)
    {
        // A job cancelled earlier keeps its row but not its payload. There is nothing to match on,
        // and deserializing an empty string throws — which would break this mutation for every
        // other RefTest in the table, not just this one.
        if (string.IsNullOrEmpty(job.Payload))
            return false;

        return job.JobType switch
        {
            JobType.InvitationEmail =>
                JsonSerializer.Deserialize<InvitationEmailPayload>(job.Payload, _jsonOptions)?.RefTestId == refTestId,
            JobType.ResultEmail =>
                JsonSerializer.Deserialize<ResultEmailPayload>(job.Payload, _jsonOptions)?.RefTestId == refTestId,
            JobType.RefTestExpiration =>
                JsonSerializer.Deserialize<RefTestExpirationPayload>(job.Payload, _jsonOptions)?.RefTestId == refTestId,
            _ => false
        };
    }
}