using Handball.Belgium.RefTestManagement.Domain.Events;

namespace Handball.Belgium.RefTestManagement.Domain.Privacy.Events;

/// <summary>Records that a challenge email was accepted by the configured provider.</summary>
public sealed record PersonalDataExportChallengeEmailDeliveredEvent(int Attempt) : DomainEventBase
{
    public const string EventType = "PersonalDataExportChallengeEmailDelivered";

    public override string ActionName => EventType;

    public override object GetChanges() => new { attempt = Attempt, accepted = true };
}

/// <summary>Records a retryable failure to deliver a mailbox-verification challenge.</summary>
public sealed record PersonalDataExportChallengeEmailDeliveryFailedEvent(int Attempt) : DomainEventBase
{
    public const string EventType = "PersonalDataExportChallengeEmailDeliveryFailed";

    public override string ActionName => EventType;

    public override object GetChanges() => new { attempt = Attempt, accepted = false };
}

/// <summary>Records that a participant proved control of the mailbox for a data export request.</summary>
public sealed record PersonalDataExportRequestVerifiedEvent(bool ChallengeEmailWasAccepted) : DomainEventBase
{
    public const string EventType = "PersonalDataExportRequestVerified";

    public override string ActionName => EventType;

    public override object GetChanges() => new
    {
        mailboxVerified = true,
        challengeEmailWasAccepted = ChallengeEmailWasAccepted
    };
}

/// <summary>Describes a safe, non-sensitive reason an export could not be completed.</summary>
public enum PersonalDataExportDeliveryFailureCode
{
    DeliveryFailed,
    SizeLimitExceeded,
    NoCurrentRecords,
    DeliveryOutcomeUnknown
}

/// <summary>Records that all export PDF attachments were accepted for delivery.</summary>
public sealed record PersonalDataExportDeliveredEvent(int Attempt, int AttachmentCount) : DomainEventBase
{
    public const string EventType = "PersonalDataExportDelivered";

    public override string ActionName => EventType;

    public override object GetChanges() => new
    {
        attempt = Attempt,
        accepted = true,
        attachmentCount = AttachmentCount
    };
}

/// <summary>Records a failed export delivery attempt without recipient or provider details.</summary>
public sealed record PersonalDataExportDeliveryFailedEvent(
    int Attempt,
    PersonalDataExportDeliveryFailureCode FailureCode,
    bool IsTerminal) : DomainEventBase
{
    public const string EventType = "PersonalDataExportDeliveryFailed";

    public override string ActionName => EventType;

    public override object GetChanges() => new
    {
        attempt = Attempt,
        accepted = FailureCode == PersonalDataExportDeliveryFailureCode.DeliveryOutcomeUnknown
            ? (bool?)null
            : false,
        terminal = IsTerminal,
        failureCode = FailureCode.ToString()
    };
}
