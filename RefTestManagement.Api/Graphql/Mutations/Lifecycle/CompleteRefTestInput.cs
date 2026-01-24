namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;

public record CompleteRefTestInput(
    string Token,
    List<string> SelectedAnswerIds,
    string? Language = null
);



