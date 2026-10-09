using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

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
    /// payload is cleared) and redacts personal data in place, both on the record itself and in
    /// its audit trail. It also removes rejection text from related approval-decision payloads,
    /// including completed jobs. The RefTest record and its (redacted) audit trail are always
    /// kept for accountability — this never deletes the row itself. Idempotent: a repeat call
    /// repairs any legacy rejection text for this RefTest without sweeping other anonymized
    /// records. Used by the public self-service "withdraw consent" flow, and by a staff delete
    /// before the row is removed.
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
    /// Revalidates retention eligibility in the erasure transaction before anonymizing the
    /// current persisted record. Returns false when the candidate is missing or no longer due.
    /// </summary>
    Task<bool> EraseIfDueForRetentionAsync(
        Guid refTestId,
        DateTime cutoff,
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
