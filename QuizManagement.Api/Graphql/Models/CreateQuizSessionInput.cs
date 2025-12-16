using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public record CreateBulkQuizSessionsInput(
    Title Title,
    List<User> Users,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool SendAutomatedInvitations = false,
    bool SendAutomatedResults = false
);

public record User(string FirstName, string LastName, string Email);

[OneOf]
public record Title([property: ID<QuizTitle>]Guid? Id, string? Name);