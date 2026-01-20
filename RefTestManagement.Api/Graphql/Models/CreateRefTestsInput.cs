using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

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

public record User(string FirstName, string LastName, string Email);

[OneOf]
public record Title([property: ID<RefTestTitleDto>]Guid? Id, string? Name);