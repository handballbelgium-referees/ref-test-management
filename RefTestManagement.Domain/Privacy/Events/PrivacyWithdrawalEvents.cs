using Handball.Belgium.RefTestManagement.Domain.Events;

namespace Handball.Belgium.RefTestManagement.Domain.Privacy.Events;

/// <summary>Records a withdrawal challenge request without recipient or key material.</summary>
public sealed record PrivacyWithdrawalChallengeCreatedEvent(int MatchingRefTestCount) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalChallengeCreated";

    public override string ActionName => EventType;

    public override object GetChanges() => new { matchingRefTestCount = MatchingRefTestCount };
}

/// <summary>Records an accepted withdrawal challenge email using only its attempt count.</summary>
public sealed record PrivacyWithdrawalChallengeEmailDeliveredEvent(int Attempt) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalChallengeEmailDelivered";

    public override string ActionName => EventType;

    public override object GetChanges() => new { attempt = Attempt, accepted = true };
}

/// <summary>Records a retryable withdrawal challenge email failure using only its attempt count.</summary>
public sealed record PrivacyWithdrawalChallengeEmailDeliveryFailedEvent(int Attempt) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalChallengeEmailDeliveryFailed";

    public override string ActionName => EventType;

    public override object GetChanges() => new { attempt = Attempt, accepted = false };
}

/// <summary>Records mailbox-verified batch completion using target counts only.</summary>
public sealed record PrivacyWithdrawalBatchConfirmedEvent(int TargetCount) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalBatchConfirmed";

    public override string ActionName => EventType;

    public override object GetChanges() => new { targetCount = TargetCount };
}

/// <summary>Records that every durable target in a withdrawal batch finished.</summary>
public sealed record PrivacyWithdrawalBatchCompletedEvent(int TargetCount) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalBatchCompleted";

    public override string ActionName => EventType;

    public override object GetChanges() => new { completedTargetCount = TargetCount };
}
