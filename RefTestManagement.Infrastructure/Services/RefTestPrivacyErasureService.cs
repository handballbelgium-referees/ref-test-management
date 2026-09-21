using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.EntityFrameworkCore;
// Aliased: this namespace also has its own RefTestStartedEvent/RefTestCompletedEvent records
// (see RefTestSubscriptionService.cs) used only for publishing GraphQL subscriptions — distinct
// from the domain/audit events of the same name below.
using DomainEvents = Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Who asked for a RefTest's personal data to be erased. This decides one thing: whether the
/// actor recorded on the anonymization audit event is the participant's own identity — which is
/// personal data and must be redacted — or an accountable staff/system identity, which is the
/// record of who performed the erasure and must survive it.
/// </summary>
public enum ErasureInitiator
{
    /// <summary>
    /// The participant themselves, through the anonymous token-based withdraw-consent flow.
    /// The audit interceptor attributes the event to the RefTest holder, so the actor is
    /// personal data.
    /// </summary>
    Participant,

    /// <summary>
    /// A signed-in administrator deleting the record, or the automated retention service. The
    /// actor is the administrator's identity or "System" — accountability information, not the
    /// participant's.
    /// </summary>
    Operator
}

public interface IRefTestPrivacyErasureService
{
    /// <summary>
    /// Erases a RefTest's personal data: cancels any background job referencing it that is still
    /// pending or in flight (so no invitation/result email goes out afterwards, and the job's
    /// payload — which carries name, email and token — is cleared) and redacts personal data
    /// (name, email, token) in place, both on the record itself and in its audit trail. The
    /// RefTest record and its (redacted) audit trail are always kept for accountability — this
    /// never deletes the row itself. Idempotent: calling it again on an already-anonymized
    /// RefTest is a no-op. Used by the public self-service "withdraw consent" flow, and by a
    /// staff delete before the row is removed.
    /// </summary>
    /// <param name="refTest">The RefTest to erase.</param>
    /// <param name="initiator">
    /// Who requested the erasure. Governs whether the anonymization event's recorded actor is
    /// redacted — see <see cref="ErasureInitiator"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EraseAsync(
        RefTest refTest,
        ErasureInitiator initiator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Erases a RefTest's personal data and then removes the row, as one atomic unit. This is the
    /// operation a staff "Delete" performs: it cancels any background job referencing the RefTest,
    /// redacts personal data from the record and its audit trail, then deletes the record.
    /// <para>
    /// Erasing is not optional and not a separate step callers may skip: audit events carry no FK
    /// to the RefTest and are not removed with it, so deleting the row on its own would leave the
    /// participant's name and email in the audit trail until audit retention expires them.
    /// </para>
    /// </summary>
    Task EraseAndDeleteAsync(
        RefTest refTest,
        ErasureInitiator initiator,
        CancellationToken cancellationToken = default);
}

public sealed class RefTestPrivacyErasureService(RefTestManagementContext context) : IRefTestPrivacyErasureService
{
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

            if (!refTest.IsAnonymized)
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
        // invitation/result email) so nothing gets sent out referencing a record that's
        // about to be gone. The helper also handles report jobs written before RefTestId was
        // added to their payload.
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
        if (refTest.IsAnonymized)
            return;

        var strategy = context.Database.CreateExecutionStrategy();

        // The retrying execution strategy must own the transaction as a single retriable unit.
        // Each attempt reloads refTest fresh from the database first: if a prior attempt already
        // called Anonymize() in memory but its transaction was rolled back by a transient
        // failure (e.g. during the second SaveChangesAsync below or the final commit), refTest
        // would otherwise still look "already anonymized" on retry — EF would then see no
        // changes to persist for it, and the erasure would never actually reach the database,
        // even though the audit redaction (touching unrelated, already-committed rows) could
        // still succeed and make it look like the whole operation worked.
        await strategy.ExecuteAsync(cancellationToken, async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            await context.Entry(refTest).ReloadAsync(ct);
            if (refTest.IsAnonymized)
            {
                await transaction.CommitAsync(ct);
                return;
            }

            await EraseCoreAsync(refTest, initiator, ct);

            await transaction.CommitAsync(ct);
        });
    }

    /// <summary>
    /// Redacts the RefTest and its audit trail in place and cancels any job still referencing it.
    /// Assumes an ambient transaction owned by the caller, and a RefTest that is not yet
    /// anonymized.
    /// </summary>
    private async Task EraseCoreAsync(RefTest refTest, ErasureInitiator initiator, CancellationToken ct)
    {
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
        // Completed jobs are left untouched and follow normal retention cleanup.
        var cancellableJobs = await FindCancellableJobsAsync(refTest.Id, ct);

        foreach (var job in cancellableJobs)
            job.Cancel("RefTest personal data was erased");

        // Redact personal data in place, keep the record and its audit trail for accountability.
        refTest.Anonymize();
        await context.SaveChangesAsync(ct);

        var auditEvents = await context.AuditEvents
            .Where(e => e.StreamId == refTestId)
            .ToListAsync(ct);

        foreach (var auditEvent in auditEvents)
        {
            var redacted = AuditPiiRedactor.RedactData(auditEvent.Data);
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

        await context.SaveChangesAsync(ct);
    }

    private async Task<List<Job>> FindCancellableJobsAsync(Guid refTestId, CancellationToken ct)
    {
        var id = refTestId.ToString();
        var candidates = await context.Jobs
            .Where(job => (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
                          && (job.Payload.Contains(id) || job.JobType == JobType.ReportEmail))
            .ToListAsync(ct);

        return candidates
            .Where(job => job.Payload.Contains(id, StringComparison.OrdinalIgnoreCase)
                          || (job.JobType == JobType.ReportEmail && IsLegacyReportPayload(job.Payload)))
            .ToList();
    }

    private static bool IsLegacyReportPayload(string payload)
        => payload.IndexOf("\"refTestId\"", StringComparison.OrdinalIgnoreCase) < 0;
}
