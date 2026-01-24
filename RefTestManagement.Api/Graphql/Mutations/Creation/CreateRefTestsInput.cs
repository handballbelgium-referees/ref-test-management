using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

public record CreateBulkRefTestsInput(
    Title Title,
    List<User> Users,
    int NumberOfQuestions,
    bool RandomQuestionsForEachUser,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool SendAutomatedInvitations = false,
    bool SendAutomatedResults = false
);

[OneOf]
public record Title([property: ID<RefTestTitle>]Guid? Id, string? Name);

