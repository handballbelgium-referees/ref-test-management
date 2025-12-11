namespace QuizManagement.Api.Graphql.Models;

public record CreateBulkQuizSessionsInput(
    List<User> Users,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool SendInvitations = false
);

public record User(string FirstName, string LastName, string Email);