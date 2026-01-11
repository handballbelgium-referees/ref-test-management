namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record SaveRefTestProgressInput(
    string Token,
    int CurrentQuestionIndex,
    List<string> SelectedAnswerIds,
    string Language
);

