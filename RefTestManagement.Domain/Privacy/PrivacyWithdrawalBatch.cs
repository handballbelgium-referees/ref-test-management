using Handball.Belgium.RefTestManagement.Domain.Events;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;

namespace Handball.Belgium.RefTestManagement.Domain.Privacy;

/// <summary>
/// Durable work record for one mailbox-verified bulk consent withdrawal.
/// </summary>
/// <remarks>
/// The batch stores only lifecycle metadata and counts. Its RefTest IDs live in separate target
/// rows so the job payload can remain batch-ID-only and target rows can be removed after completion.
/// </remarks>
public sealed class PrivacyWithdrawalBatch : IHasDomainEvents, IHasVerifiedParticipantActor
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private PrivacyWithdrawalBatch()
    {
    }

    private PrivacyWithdrawalBatch(DateTime createdAt, int targetCount)
    {
        Id = Guid.NewGuid();
        CreatedAt = createdAt;
        TargetCount = targetCount;
        _domainEvents.Add(new PrivacyWithdrawalBatchConfirmedEvent(targetCount)
        {
            OccurredAt = createdAt
        });
    }

    public Guid Id { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public int TargetCount { get; private set; }
    /// <summary>The last durable worker job committed for this batch.</summary>
    public Guid? LatestJobId { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <inheritdoc />
    public bool IsVerifiedParticipantActor => true;

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Creates a verified batch with count-only audit evidence.</summary>
    public static PrivacyWithdrawalBatch Create(DateTime createdAt, int targetCount)
    {
        if (targetCount < 0)
            throw new ArgumentOutOfRangeException(nameof(targetCount));

        return new PrivacyWithdrawalBatch(createdAt, targetCount);
    }

    /// <summary>Records a durable worker job so concurrent recovery cannot commit duplicates.</summary>
    public bool MarkJobEnqueued(Guid jobId)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("A privacy-withdrawal job id cannot be empty.", nameof(jobId));
        if (LatestJobId == jobId)
            return false;

        LatestJobId = jobId;
        return true;
    }

    /// <summary>Marks the batch terminal after all targets completed or exhausted their retries.</summary>
    public bool MarkCompleted(DateTime completedAt, int exhaustedTargetCount = 0)
    {
        if (exhaustedTargetCount < 0 || exhaustedTargetCount > TargetCount)
            throw new ArgumentOutOfRangeException(nameof(exhaustedTargetCount));
        if (CompletedAt is not null)
            return false;

        CompletedAt = completedAt;
        _domainEvents.Add(new PrivacyWithdrawalBatchCompletedEvent(
            TargetCount - exhaustedTargetCount,
            exhaustedTargetCount)
        {
            OccurredAt = completedAt
        });
        return true;
    }

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();
}
