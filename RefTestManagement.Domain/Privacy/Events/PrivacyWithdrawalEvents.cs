using Handball.Belgium.RefTestManagement.Domain.Events;
using Handball.Belgium.RefTestManagement.Domain.Privacy;

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

/// <summary>Records a verified withdrawal confirmation when no new work was queued.</summary>
public sealed record PrivacyWithdrawalChallengeConfirmedWithoutWorkEvent : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalChallengeConfirmedWithoutWork";

    public override string ActionName => EventType;

    public override object? GetChanges() => null;
}

/// <summary>Records mailbox-verified batch completion using target counts only.</summary>
public sealed record PrivacyWithdrawalBatchConfirmedEvent(int TargetCount) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalBatchConfirmed";

    public override string ActionName => EventType;

    public override object GetChanges() => new { targetCount = TargetCount };
}

/// <summary>Records completed and retry-exhausted targets in a terminal withdrawal batch.</summary>
public sealed record PrivacyWithdrawalBatchCompletedEvent(
    int CompletedTargetCount,
    int ExhaustedTargetCount) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalBatchCompleted";

    public override string ActionName => EventType;

    public override object GetChanges() => new
    {
        completedTargetCount = CompletedTargetCount,
        exhaustedTargetCount = ExhaustedTargetCount
    };
}

/// <summary>Records an operator acknowledgement using only the sanitized failure category.</summary>
public sealed record PrivacyWithdrawalFailedTargetAcknowledgedEvent(
    PrivacyWithdrawalTargetFailureCode FailureCategory) : DomainEventBase
{
    public const string EventType = "PrivacyWithdrawalFailedTargetAcknowledged";

    public override string ActionName => EventType;

    public override object GetChanges() => new { failureCategory = FailureCategory.ToString() };
}
