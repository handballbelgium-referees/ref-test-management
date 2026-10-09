using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

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
/// <see cref="IJobPersistenceContext.SaveChangesWithRetryAsync"/> commit the entity and the job
/// in a single transaction. Otherwise a failure between the two writes leaves a persisted RefTest
/// whose invitation is never sent.
/// </para>
/// <para>
/// Callers that need the job staged into some other unit of work can pass that
/// <see cref="IJobPersistenceContext"/> explicitly; otherwise the service uses its injected
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
