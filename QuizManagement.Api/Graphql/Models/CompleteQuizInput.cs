namespace QuizManagement.Api.Graphql.Models;

public record CompleteQuizInput(
    string Token,
    List<string> QuestionIds,
    List<string> SelectedAnswerIds
);

