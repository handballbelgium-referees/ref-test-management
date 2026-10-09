using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Update;

public sealed record UpdateRefTestDetailsCommand(
    Guid Id, string FirstName, string LastName, string Email, bool ResendInvitation);

public sealed record UpdateRefTestConfigurationCommand(
    Guid Id,
    Guid? TitleId,
    string? TitleName,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers,
    bool RandomQuestions);

public sealed record UpdateRefTestNotificationSettingsCommand(
    Guid Id,
    bool? SendInvitationsAutomatically,
    bool? SendResultsAutomatically);

public sealed record ExtendRefTestTimeCommand(Guid Id, int AdditionalMinutes);

public sealed record RegenerateRefTestTokenCommand(Guid Id);
