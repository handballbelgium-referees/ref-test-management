using Handball.Belgium.RefTestManagement.AuditLog;

namespace Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

public sealed record RefTestDetailsUpdatedEvent(
    string OldFirstName,
    string NewFirstName,
    string OldLastName,
    string NewLastName,
    string OldEmail,
    string NewEmail) : DomainEventBase
{
    public override string ActionName => "RefTestDetailsUpdated";

    public override object? GetChanges() => new
    {
        firstName = new { old = OldFirstName, @new = NewFirstName },
        lastName = new { old = OldLastName, @new = NewLastName },
        email = new { old = OldEmail, @new = NewEmail }
    };
}

public sealed record RefTestConfigurationUpdatedEvent(
    Guid OldTitleId,
    Guid NewTitleId,
    int OldNumberOfQuestions,
    int NewNumberOfQuestions,
    int OldMaxTimeInMinutes,
    int NewMaxTimeInMinutes,
    int QuestionPoolCount) : DomainEventBase, IDomainEventWithResolution
{
    public override string ActionName => "RefTestConfigurationUpdated";

    public override object? GetChanges() => GetChanges(static (_, _) => null);

    public object? GetChanges(Func<string, Guid, string?> entityResolver) => new
    {
        title = new { old = entityResolver("RefTestTitle", OldTitleId), @new = entityResolver("RefTestTitle", NewTitleId) },
        numberOfQuestions = new { old = OldNumberOfQuestions, @new = NewNumberOfQuestions },
        maxTimeInMinutes = new { old = OldMaxTimeInMinutes, @new = NewMaxTimeInMinutes },
        questionPoolCount = QuestionPoolCount
    };
}

public sealed record RefTestNotificationSettingsUpdatedEvent(
    bool? OldSendInvitationsAutomatically,
    bool? NewSendInvitationsAutomatically,
    bool? OldSendResultsAutomatically,
    bool? NewSendResultsAutomatically) : DomainEventBase
{
    public override string ActionName => "RefTestNotificationSettingsUpdated";

    public override object? GetChanges() => new
    {
        sendInvitationsAutomatically = OldSendInvitationsAutomatically != NewSendInvitationsAutomatically
            ? new { old = OldSendInvitationsAutomatically, @new = NewSendInvitationsAutomatically }
            : (object?)null,
        sendResultsAutomatically = OldSendResultsAutomatically != NewSendResultsAutomatically
            ? new { old = OldSendResultsAutomatically, @new = NewSendResultsAutomatically }
            : (object?)null
    };
}

public sealed record RefTestTimeExtendedEvent(
    int AdditionalMinutes,
    int OldMaxTimeInMinutes,
    int NewMaxTimeInMinutes) : DomainEventBase
{
    public override string ActionName => "RefTestTimeExtended";

    public override object? GetChanges() => new
    {
        additionalMinutes = AdditionalMinutes,
        maxTimeInMinutes = new { old = OldMaxTimeInMinutes, @new = NewMaxTimeInMinutes }
    };
}

public sealed record RefTestSoftResetEvent(bool TokenRegenerated) : DomainEventBase
{
    public override string ActionName => "RefTestSoftReset";
    public override object? GetChanges() => new { tokenRegenerated = TokenRegenerated };
}
