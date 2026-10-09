namespace Handball.Belgium.RefTestManagement.Application.RefTests.Creation;

public sealed record CreateRefTestsCommand(
    Guid? TitleId,
    string? TitleName,
    IReadOnlyList<CreateRefTestUser> Users,
    int NumberOfQuestions,
    bool RandomQuestionsForEachUser,
    int MaxTimeInMinutes,
    IReadOnlyList<string>? SpecificQuestionNumbers,
    bool SendAutomatedInvitations,
    bool SendAutomatedResults,
    DateTime? ScheduledAt,
    bool RequiresApproval);

public sealed record CreateRefTestUser(string FirstName, string LastName, string Email);
