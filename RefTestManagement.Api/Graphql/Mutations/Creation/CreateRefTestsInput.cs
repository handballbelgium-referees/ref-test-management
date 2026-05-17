using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

public record CreateRefTestsInput(
    Title Title,
    List<User> Users,
    int NumberOfQuestions,
    bool RandomQuestionsForEachUser,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool SendAutomatedInvitations = false,
    bool SendAutomatedResults = false,
    /// <summary>
    /// Optional date/time from which the ref tests can be started.
    /// When SendAutomatedInvitations is true, the invitation email is scheduled for this time.
    /// </summary>
    DateTime? ScheduledAt = null
);