using Handball.Belgium.RefTestManagement.AuditLog;

namespace Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

public sealed record RefTestCreatedEvent(
    string FirstName,
    string LastName,
    string Email,
    Guid TitleId,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    bool SendInvitationsAutomatically,
    bool SendResultsAutomatically,
    bool RequiresApproval) : DomainEventBase, IDomainEventWithResolution
{
    public override string ActionName => "RefTestCreated";

    public override object? GetChanges() => GetChanges(static (_, _) => null);

    public object? GetChanges(Func<string, Guid, string?> entityResolver) => new
    {
        title = entityResolver("RefTestTitle", TitleId),
        firstName = FirstName,
        lastName = LastName,
        email = Email,
        numberOfQuestions = NumberOfQuestions,
        maxTimeInMinutes = MaxTimeInMinutes,
        sendInvitationsAutomatically = SendInvitationsAutomatically,
        sendResultsAutomatically = SendResultsAutomatically,
        requiresApproval = RequiresApproval
    };
}
