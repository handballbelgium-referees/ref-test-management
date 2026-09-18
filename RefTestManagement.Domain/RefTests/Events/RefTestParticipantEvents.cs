using Handball.Belgium.RefTestManagement.Domain.Events;

namespace Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

/// <summary>
/// Raised when the participant (holder of the invitation token) starts a RefTest. These
/// participant-triggered events carry the participant's own name/email in the payload since
/// the requester is anonymous (not an authenticated staff user, not the system) — the audit
/// interceptor also attributes the ActorName/ActorEmail columns to this same identity.
/// </summary>
public sealed record RefTestStartedEvent(
    string FirstName,
    string LastName,
    string Email) : DomainEventBase
{
    public const string EventType = "RefTestStarted";

    public override string ActionName => EventType;

    public override object? GetChanges() => new
    {
        firstName = FirstName,
        lastName = LastName,
        email = Email
    };
}

/// <summary>
/// Raised when the participant explicitly accepts the currently published privacy notice.
/// </summary>
public sealed record RefTestPrivacyNoticeAcceptedEvent(
    string NoticeVersion,
    string FirstName,
    string LastName,
    string Email) : DomainEventBase
{
    public const string EventType = "RefTestPrivacyNoticeAccepted";

    public override string ActionName => EventType;

    public override object? GetChanges() => new
    {
        noticeVersion = NoticeVersion,
        firstName = FirstName,
        lastName = LastName,
        email = Email
    };
}

/// <summary>
/// Raised when the participant completes a RefTest, recording the final score.
/// </summary>
public sealed record RefTestCompletedEvent(
    int QuestionScore,
    int AnswerScore,
    int AnswerTotal,
    double Percentage,
    string FirstName,
    string LastName,
    string Email) : DomainEventBase
{
    public const string EventType = "RefTestCompleted";

    public override string ActionName => EventType;

    public override object? GetChanges() => new
    {
        questionScore = QuestionScore,
        answerScore = AnswerScore,
        answerTotal = AnswerTotal,
        percentage = Percentage,
        firstName = FirstName,
        lastName = LastName,
        email = Email
    };
}

