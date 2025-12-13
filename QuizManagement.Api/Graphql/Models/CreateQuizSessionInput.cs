namespace QuizManagement.Api.Graphql.Models;

public record CreateBulkQuizSessionsInput(
    Title Title,
    List<User> Users,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool SendInvitations = false
);

public record User(string FirstName, string LastName, string Email);

[OneOf]
public record Title(Guid? Id, string? Name);