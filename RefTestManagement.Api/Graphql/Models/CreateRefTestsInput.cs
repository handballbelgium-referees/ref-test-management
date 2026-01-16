using Handball.Belgium.RefTestManagement.Domain;

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
public record Title([ID<RefTestTitle>]Guid? Id, string? Name);