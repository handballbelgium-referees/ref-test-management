using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
// Aliased: this namespace also has its own RefTestStartedEvent/RefTestCompletedEvent records
// (see RefTestSubscriptionService.cs) used only for publishing GraphQL subscriptions — distinct
// from the domain/audit events of the same name below.
using DomainEvents = Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public sealed class RefTestPrivacyErasureService(RefTestManagementContext context) : IRefTestPrivacyErasureService
{
    private static readonly JsonSerializerOptions JobPayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Event types raised for anonymous, token-based participant actions (see
    /// <see cref="AuditSaveChangesInterceptor"/>), whose ActorName/ActorEmail are attributed to
    /// the RefTest holder's own identity rather than "System". These are always redacted on
    /// erasure since the streamId already scopes them to this RefTest — no need to match names.
    /// </summary>
    private static readonly HashSet<string> ParticipantActorEventTypes =
    [
        DomainEvents.RefTestStartedEvent.EventType,
        DomainEvents.RefTestPrivacyNoticeAcceptedEvent.EventType,
        DomainEvents.RefTestCompletedEvent.EventType
    ];

    public async Task EraseAndDeleteAsync(
        RefTest refTest,
        ErasureInitiator initiator,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        // One transaction for both halves. Run separately they commit independently, and a delete
        // that fails after the erasure committed leaves behind an anonymized row nobody can
        // identify or retry meaningfully.
        await strategy.ExecuteAsync(cancellationToken, async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // See EraseAsync for why every attempt starts from a freshly loaded entity.
            await context.Entry(refTest).ReloadAsync(ct);

            await EraseCoreAsync(refTest, initiator, ct);

            await DeleteCoreAsync(refTest, ct);

            await transaction.CommitAsync(ct);
        });
    }

    /// <summary>
    /// Removes the RefTest row and cancels any job still referencing it. Assumes an ambient
    /// transaction owned by the caller.
    /// </summary>
    private async Task DeleteCoreAsync(RefTest refTest, CancellationToken ct)
    {
        // Cancel any job still waiting to run or already in flight (e.g. a scheduled
        // invitation/result/report email) so nothing gets sent out referencing a record that's
        // about to be gone. Report payloads are required to carry RefTestId for this association.
        var cancellableJobs = await FindCancellableJobsAsync(refTest.Id, ct);

        foreach (var job in cancellableJobs)
            job.Cancel("RefTest was deleted");

        refTest.MarkDeleted();
        context.RefTests.Remove(refTest);
        await context.SaveChangesAsync(ct);
    }

    public async Task EraseAsync(
        RefTest refTest,
        ErasureInitiator initiator,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        // The retrying execution strategy must own the transaction as a single retriable unit.
        // Each attempt reloads refTest fresh from the database first: a previous attempt may
        // already have cleared its rejection reason in memory even though the transaction rolled
        // back, so the retry must compare against the persisted state before doing the repair.
        await strategy.ExecuteAsync(cancellationToken, async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            await context.Entry(refTest).ReloadAsync(ct);

            await EraseCoreAsync(refTest, initiator, ct);

            await transaction.CommitAsync(ct);
        });
    }

    public async Task<bool> EraseIfDueForRetentionAsync(
        Guid refTestId,
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(cancellationToken, async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var refTest = await context.RefTests
                .SingleOrDefaultAsync(candidate => candidate.Id == refTestId, ct);
            if (refTest is null)
                return false;

            // The caller's candidate list may be stale, and this context may already track an
            // older version. Refresh within the transaction before deciding whether erasure is
            // still lawful; the concurrency token protects the subsequent write from a racing edit.
            await context.Entry(refTest).ReloadAsync(ct);
            if (context.Entry(refTest).State == EntityState.Detached
                || !PrivacyRetentionQueries.IsDueForErasure(cutoff).Compile()(refTest))
                return false;

            await EraseCoreAsync(refTest, ErasureInitiator.Operator, ct);
            await transaction.CommitAsync(ct);
            return true;
        });
    }

    /// <summary>
    /// Redacts the RefTest and its audit trail in place, and cancels any job still referencing it.
    /// Also repairs legacy rejection copies when the RefTest is already anonymized. Assumes an
    /// ambient transaction owned by the caller.
    /// </summary>
    private async Task EraseCoreAsync(RefTest refTest, ErasureInitiator initiator, CancellationToken ct)
    {
        var wasAlreadyAnonymized = refTest.IsAnonymized;
        var erasedParticipantEmail = refTest.Email;

        // The anonymization event is attributed to whoever triggered the erasure. For the
        // participant's own withdraw-consent request that is the RefTest holder — by the time
        // the event is written, Anonymize() has already replaced the name and email in memory,
        // so the captured actor is the erased placeholder identity and must be redacted like
        // any other participant-attributed event. For a staff delete or the retention service
        // it is the administrator or "System", and redacting it would destroy the only record
        // of who performed the erasure.
        HashSet<string> participantActorEventTypes = initiator == ErasureInitiator.Participant
            ? [.. ParticipantActorEventTypes, DomainEvents.RefTestAnonymizedEvent.EventType]
            : ParticipantActorEventTypes;

        var refTestId = refTest.Id.ToString();

        // Cancel any job still waiting to run or already in flight (e.g. a scheduled
        // invitation/result email) so nothing gets sent out referencing the erased data.
        // Completed jobs are not cancelled; related approval-decision payloads are scrubbed below.
        var cancellableJobs = await FindCancellableJobsAsync(refTest.Id, ct);

        foreach (var job in cancellableJobs)
            job.Cancel("RefTest personal data was erased");

        // Redact personal data in place, keep the record and its audit trail for accountability.
        refTest.Anonymize();
        await context.SaveChangesAsync(ct);

        // An already-anonymized row no longer has the participant's real address with which to
        // find these separate requests. They were cleared during the original erasure.
        if (!wasAlreadyAnonymized)
        {
            await ClearExportRequestsForErasedParticipantAsync(erasedParticipantEmail, ct);
            await ClearPrivacyWithdrawalChallengesForErasedParticipantAsync(erasedParticipantEmail, ct);
        }

        var auditEvents = await context.AuditEvents
            .Where(e => e.StreamId == refTestId)
            .ToListAsync(ct);

        foreach (var auditEvent in auditEvents)
        {
            var redacted = AuditPiiRedactor.RedactData(auditEvent.Data, auditEvent.Type);
            if (redacted != auditEvent.Data)
                context.Entry(auditEvent).Property(e => e.Data).CurrentValue = redacted;

            // Participant-triggered events (started, privacy notice accepted, completed) are
            // attributed to the RefTest holder's own name/email instead of "System". Since
            // these events are already scoped to this RefTest by StreamId, no name/email
            // matching is needed — just redact by event Type. Staff/system actor
            // attribution on other event types is left untouched.
            if (!participantActorEventTypes.Contains(auditEvent.Type))
                continue;

            context.Entry(auditEvent).Property(e => e.ActorName).CurrentValue = AuditPiiRedactor.RedactedValue;
            context.Entry(auditEvent).Property(e => e.ActorEmail).CurrentValue = AuditPiiRedactor.RedactedValue;
        }

        await RedactApprovalDecisionJobPayloadsAsync(refTest.Id, ct);
        await context.SaveChangesAsync(ct);
    }

    private async Task ClearExportRequestsForErasedParticipantAsync(string participantEmail, CancellationToken ct)
    {
        var normalizedEmail = participantEmail.ToUpperInvariant();
        var requests = await context.PersonalDataExportRequests
            .Where(request => request.Email != string.Empty && request.Email.ToUpper() == normalizedEmail)
            .ToListAsync(ct);
        if (requests.Count == 0)
            return;

        // An export request covers all records for this mailbox. Erasing any one record
        // invalidates the pending snapshot so a delivery worker cannot send pre-erasure data.
        var requestIds = requests.Select(request => request.Id).ToHashSet();
        foreach (var request in requests)
            request.ClearForPrivacyErasure();

        // Both export jobs carry only the request ID. Cancel them as part of the same unit of
        // work so an erasure cannot leave a retryable challenge or data delivery behind.
        var exportJobs = await context.Jobs
            .Where(job => (job.JobType == JobType.PersonalDataExportChallengeEmail
                           || job.JobType == JobType.PersonalDataExportDeliveryEmail)
                          && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing))
            .ToListAsync(ct);

        foreach (var job in exportJobs)
        {
            if (string.IsNullOrEmpty(job.Payload))
                continue;

            try
            {
                var requestId = job.JobType switch
                {
                    JobType.PersonalDataExportChallengeEmail =>
                        JsonSerializer.Deserialize<PersonalDataExportChallengeEmailPayload>(
                            job.Payload, JobPayloadOptions)?.RequestId,
                    JobType.PersonalDataExportDeliveryEmail =>
                        JsonSerializer.Deserialize<PersonalDataExportDeliveryEmailPayload>(
                            job.Payload, JobPayloadOptions)?.RequestId,
                    _ => null
                };
                if (requestId.HasValue && requestIds.Contains(requestId.Value))
                    job.Cancel("Participant personal data was erased");
            }
            catch (JsonException)
            {
                // An unrelated malformed job must not prevent this erasure from completing.
            }
        }
    }

    private async Task ClearPrivacyWithdrawalChallengesForErasedParticipantAsync(string participantEmail, CancellationToken ct)
    {
        var normalizedEmail = PrivacyWithdrawalChallenge.NormalizeEmail(participantEmail);
        var normalizedEmailHash = PrivacyWithdrawalChallenge.HashNormalizedEmail(normalizedEmail);
        var challenges = await context.PrivacyWithdrawalChallenges
            .Where(challenge => challenge.NormalizedEmailHash == normalizedEmailHash
                                && challenge.Email != string.Empty)
            .ToListAsync(ct);
        if (challenges.Count == 0)
            return;

        var challengeIds = challenges.Select(challenge => challenge.Id).ToHashSet();
        foreach (var challenge in challenges)
            challenge.ClearForPrivacyErasure();

        // Withdrawal challenge jobs carry only the challenge ID. A confirmed batch has already
        // cleared its address and is intentionally not selected here, so erasing its first target
        // cannot clear the remaining durable targets or cancel the batch job.
        var challengeJobs = await context.Jobs
            .Where(job => job.JobType == JobType.PrivacyWithdrawalChallengeEmail
                          && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing))
            .ToListAsync(ct);

        foreach (var job in challengeJobs)
        {
            if (string.IsNullOrEmpty(job.Payload))
                continue;

            try
            {
                var challengeId = JsonSerializer
                    .Deserialize<PrivacyWithdrawalChallengeEmailPayload>(job.Payload, JobPayloadOptions)
                    ?.ChallengeId;
                if (challengeId.HasValue && challengeIds.Contains(challengeId.Value))
                    job.Cancel("Participant personal data was erased");
            }
            catch (JsonException)
            {
                // An unrelated malformed job must not prevent this erasure from completing.
            }
        }
    }

    private async Task RedactApprovalDecisionJobPayloadsAsync(Guid refTestId, CancellationToken ct)
    {
        var id = refTestId.ToString();

        // Keep this repair scoped to one RefTest's approval-decision jobs rather than sweeping
        // the job table. Deliberately include every status: completed and failed jobs retain their
        // payloads until normal cleanup, and may still contain the rejection text.
        var candidateJobs = await context.Jobs
            .Where(job => job.JobType == JobType.ApprovalDecisionEmail && job.Payload.Contains(id))
            .ToListAsync(ct);

        foreach (var job in candidateJobs)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<ApprovalDecisionEmailPayload>(job.Payload, JobPayloadOptions);
                if (payload is null)
                    continue;

                var redactedPayload = payload.RedactRejectionReasonFor(refTestId);
                if (ReferenceEquals(payload, redactedPayload))
                    continue;

                context.Entry(job).Property(j => j.Payload).CurrentValue =
                    JsonSerializer.Serialize(redactedPayload, JobPayloadOptions);
            }
            catch (JsonException)
            {
                // If a malformed approval payload mentions this RefTest, discard its unreadable
                // contents rather than risk retaining rejection text. Keep its lifecycle status.
                context.Entry(job).Property(j => j.Payload).CurrentValue = string.Empty;
            }
        }
    }

    private async Task<List<Job>> FindCancellableJobsAsync(Guid refTestId, CancellationToken ct)
    {
        var id = refTestId.ToString();
        var candidates = await context.Jobs
            .Where(job => (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
                          && job.Payload.Contains(id))
            .ToListAsync(ct);

        return candidates
            .Where(job => job.Payload.Contains(id, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
