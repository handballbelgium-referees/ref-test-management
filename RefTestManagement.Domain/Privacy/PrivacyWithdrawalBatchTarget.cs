using Handball.Belgium.RefTestManagement.Domain.Events;

namespace Handball.Belgium.RefTestManagement.Domain.Privacy;

/// <summary>
/// Durable per-RefTest progress for a privacy-withdrawal batch.
/// </summary>
/// <remarks>
/// Targets deliberately opt out of property-diff audit logging: their IDs are operational
/// processing state, while the parent batch records counts-only evidence.
/// </remarks>
public sealed class PrivacyWithdrawalBatchTarget : IHasDomainEvents
{
    private static readonly IReadOnlyList<IDomainEvent> NoDomainEvents = Array.Empty<IDomainEvent>();

    private PrivacyWithdrawalBatchTarget()
    {
    }

    private PrivacyWithdrawalBatchTarget(Guid batchId, Guid refTestId)
    {
        Id = Guid.NewGuid();
        BatchId = batchId;
        RefTestId = refTestId;
    }

    public Guid Id { get; private set; }
    public long Version { get; private set; } = 1;
    public Guid BatchId { get; private set; }
    public Guid RefTestId { get; private set; }
    public DateTime? ErasureStartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => NoDomainEvents;

    /// <summary>Creates a durable target that can be retried independently.</summary>
    public static PrivacyWithdrawalBatchTarget Create(Guid batchId, Guid refTestId) =>
        new(batchId, refTestId);

    /// <summary>Persists that processing began before calling the per-record erasure service.</summary>
    public bool MarkErasureStarted(DateTime startedAt)
    {
        if (ErasureStartedAt is not null || CompletedAt is not null)
            return false;

        ErasureStartedAt = startedAt;
        return true;
    }

    /// <summary>Marks this target complete after erasure and subscription publication succeed.</summary>
    public bool MarkCompleted(DateTime completedAt)
    {
        if (CompletedAt is not null)
            return false;

        CompletedAt = completedAt;
        return true;
    }

    /// <inheritdoc />
    public void ClearDomainEvents()
    {
    }
}
