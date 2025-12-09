namespace QuizManagement.Api.Graphql.Models;

public record CompleteQuizInput(
    string Token,
    List<Guid> QuestionIds,
    List<Guid> SelectedAnswerIds
);

