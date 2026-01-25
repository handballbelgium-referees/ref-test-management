namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;

public record SaveRefTestProgressInput(
    string Token,
    int CurrentQuestionIndex,
    List<string> SelectedAnswerIds,
    string Language
);



