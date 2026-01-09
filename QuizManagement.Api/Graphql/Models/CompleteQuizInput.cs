namespace QuizManagement.Api.Graphql.Models;

public record CompleteQuizInput(
    string Token,
    List<string> SelectedAnswerIds,
    string? Language = null
);

