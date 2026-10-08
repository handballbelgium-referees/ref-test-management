using Handball.Belgium.RefTestManagement.Domain.Events;

namespace Handball.Belgium.RefTestManagement.Domain.Privacy;

/// <summary>Sanitized failure categories retained for operational recovery.</summary>
public enum PrivacyWithdrawalTargetFailureCode
{
    ProcessingFailed = 1,
    AttemptLimitReached = 2
}

/// <summary>
/// Durable per-RefTest progress for a privacy-withdrawal batch.
/// </summary>
/// <remarks>
/// Targets deliberately opt out of property-diff audit logging because their identifiers are
/// operational processing state. The parent batch records sanitized acknowledgement evidence.
/// </remarks>
public sealed class PrivacyWithdrawalBatchTarget : IHasDomainEvents
{
    private static readonly TimeSpan[] RetryBackoffs =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1)
    ];

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
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? RetryExhaustedAt { get; private set; }
    public PrivacyWithdrawalTargetFailureCode? FailureCode { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>The maximum number of durable attempts for one withdrawal target.</summary>
    public const int MaximumAttempts = 5;

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

    /// <summary>Starts an attempt when the target is due and has not exhausted its retry bound.</summary>
    public bool TryStartAttempt(DateTime startedAt)
    {
        if (CompletedAt is not null
            || RetryExhaustedAt is not null
            || AttemptCount >= MaximumAttempts
            || NextAttemptAt is { } nextAttemptAt && nextAttemptAt > startedAt)
            return false;

        AttemptCount++;
        NextAttemptAt = null;
        return true;
    }

    /// <summary>
    /// Records a sanitized processing failure and either schedules bounded backoff or exhausts
    /// the target's attempt limit.
    /// </summary>
    public bool RecordProcessingFailure(DateTime failedAt)
    {
        if (CompletedAt is not null || RetryExhaustedAt is not null || AttemptCount == 0)
            return false;

        FailureCode = PrivacyWithdrawalTargetFailureCode.ProcessingFailed;
        if (AttemptCount >= MaximumAttempts)
        {
            RetryExhaustedAt = failedAt;
            NextAttemptAt = null;
        }
        else
        {
            NextAttemptAt = failedAt.Add(RetryBackoffs[AttemptCount - 1]);
        }

        return true;
    }

    /// <summary>Marks an attempt-limit reached after a worker stopped before recording failure.</summary>
    public bool MarkRetryLimitReached(DateTime failedAt)
    {
        if (CompletedAt is not null || RetryExhaustedAt is not null || AttemptCount < MaximumAttempts)
            return false;

        FailureCode = PrivacyWithdrawalTargetFailureCode.AttemptLimitReached;
        RetryExhaustedAt = failedAt;
        NextAttemptAt = null;
        return true;
    }

    /// <summary>Marks this target complete after erasure and subscription publication succeed.</summary>
    public bool MarkCompleted(DateTime completedAt)
    {
        if (CompletedAt is not null)
            return false;

        CompletedAt = completedAt;
        NextAttemptAt = null;
        return true;
    }

    /// <inheritdoc />
    public void ClearDomainEvents()
    {
    }
}
