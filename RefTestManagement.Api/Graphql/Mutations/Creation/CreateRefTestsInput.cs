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
    bool SendAutomatedResults = false
);