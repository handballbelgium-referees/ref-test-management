using System.Text.Json.Nodes;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.EntityFrameworkCore;
// Aliased: this namespace also has its own RefTestStartedEvent/RefTestCompletedEvent records
// (see RefTestSubscriptionService.cs) used only for publishing GraphQL subscriptions — distinct
// from the domain/audit events of the same name below.
using DomainEvents = Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IRefTestPrivacyErasureService
{
    /// <summary>
    /// Erases a RefTest's personal data: cancels any still-pending background jobs referencing
    /// it (so no invitation/result email goes out afterwards) and redacts personal data (name,
    /// email, token) in place, both on the record itself and in its audit trail. The RefTest
    /// record and its (redacted) audit trail are always kept for accountability — this never
    /// deletes the row itself. Idempotent: calling it again on an already-anonymized RefTest is
    /// a no-op. Used by the public self-service "withdraw consent" flow.
    /// </summary>
    Task EraseAsync(RefTest refTest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a RefTest — a real, authorized staff "Delete" action. Cancels any
    /// still-pending background jobs referencing it first, then removes the row itself. Its
    /// audit trail is left untouched (it carries no FK to the RefTest row) and expires on its
    /// own per the normal audit-log retention schedule (<see cref="AuditLogOptions.RetentionDays"/>),
    /// rather than being redacted immediately here.
    /// </summary>
    Task DeleteAsync(RefTest refTest, CancellationToken cancellationToken = default);
}

public sealed class RefTestPrivacyErasureService(RefTestManagementContext context) : IRefTestPrivacyErasureService
{
    /// <summary>Property keys whose values are considered personal data in audit event JSON payloads.</summary>
    private static readonly string[] PiiKeys = ["firstName", "lastName", "email"];
    private const string RedactedValue = "***";

    /// <summary>
    /// Event types raised for anonymous, token-based participant actions (see
    /// <see cref="AuditSaveChangesInterceptor"/>), whose ActorName/ActorEmail are attributed to
    /// the RefTest holder's own identity rather than "System". These are always redacted on
    /// erasure since the streamId already scopes them to this RefTest — no need to match names.
    /// RefTestAnonymized is included too: by the time its audit event is written, Anonymize()
    /// has already replaced FirstName/LastName/Email with erased placeholders in memory, so its
    /// captured actor is the placeholder identity rather than "***" — redact it the same way.
    /// </summary>
    private static readonly HashSet<string> ParticipantActorEventTypes =
    [
        DomainEvents.RefTestStartedEvent.EventType,
        DomainEvents.RefTestPrivacyNoticeAcceptedEvent.EventType,
        DomainEvents.RefTestCompletedEvent.EventType,
        DomainEvents.RefTestAnonymizedEvent.EventType
    ];

    public async Task DeleteAsync(RefTest refTest, CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        // The retrying execution strategy must own the transaction as a single retriable unit.
        await strategy.ExecuteAsync(cancellationToken, async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var refTestId = refTest.Id.ToString();

            // Cancel any jobs still waiting to run (e.g. a scheduled invitation/result email)
            // so nothing gets sent out referencing a record that's about to be gone.
            var pendingJobs = await context.Jobs
                .Where(job => job.Status == JobStatus.Pending && job.Payload.Contains(refTestId))
                .ToListAsync(ct);

            foreach (var job in pendingJobs)
                job.Cancel("RefTest was deleted");

            refTest.MarkDeleted();
            context.RefTests.Remove(refTest);
            await context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        });
    }

    public async Task EraseAsync(RefTest refTest, CancellationToken cancellationToken = default)
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

            var refTestId = refTest.Id.ToString();

            // Cancel any jobs still waiting to run (e.g. a scheduled invitation/result email)
            // so nothing gets sent out referencing the erased data. Jobs that already ran are
            // left untouched — they carry their own accountability value and are handled by the
            // normal retention/cleanup schedule instead.
            var pendingJobs = await context.Jobs
                .Where(job => job.Status == JobStatus.Pending && job.Payload.Contains(refTestId))
                .ToListAsync(ct);

            foreach (var job in pendingJobs)
                job.Cancel("RefTest personal data was erased");

            // Redact personal data in place, keep the record and its audit trail for accountability.
            refTest.Anonymize();
            await context.SaveChangesAsync(ct);

            var auditEvents = await context.AuditEvents
                .Where(e => e.StreamId == refTestId)
                .ToListAsync(ct);

            foreach (var auditEvent in auditEvents)
            {
                var redacted = RedactPii(auditEvent.Data);
                if (redacted != auditEvent.Data)
                    context.Entry(auditEvent).Property(e => e.Data).CurrentValue = redacted;

                // Participant-triggered events (started, privacy notice accepted, completed) are
                // attributed to the RefTest holder's own name/email instead of "System". Since
                // these events are already scoped to this RefTest by StreamId, no name/email
                // matching is needed — just redact by event Type. Staff/system actor
                // attribution on other event types is left untouched.
                if (!ParticipantActorEventTypes.Contains(auditEvent.Type))
                    continue;

                context.Entry(auditEvent).Property(e => e.ActorName).CurrentValue = RedactedValue;
                context.Entry(auditEvent).Property(e => e.ActorEmail).CurrentValue = RedactedValue;
            }

            await context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        });
    }

    /// <summary>
    /// Replaces known personal-data keys (name/email) inside an audit event's JSON <c>Data</c>
    /// payload with a redaction placeholder, leaving the rest of the event (type, timestamp,
    /// actor, other fields) intact. Handles both the flat shape used by
    /// <c>RefTestCreatedEvent</c> (e.g. <c>{"firstName":"John",...}</c>) and the old/new diff
    /// shape used by <c>RefTestDetailsUpdatedEvent</c>
    /// (e.g. <c>{"firstName":{"old":"John","new":"Jane"},...}</c>).
    /// </summary>
    private static string? RedactPii(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        if (JsonNode.Parse(data) is not JsonObject node)
            return data;

        foreach (var key in PiiKeys)
        {
            if (!node.TryGetPropertyValue(key, out var value) || value is null)
                continue;

            if (value is JsonObject diff)
            {
                foreach (var diffKey in new[] { "old", "new", "oldValue", "newValue" })
                {
                    if (diff.ContainsKey(diffKey))
                        diff[diffKey] = RedactedValue;
                }
            }
            else
            {
                node[key] = RedactedValue;
            }
        }

        return node.ToJsonString();
    }
}

