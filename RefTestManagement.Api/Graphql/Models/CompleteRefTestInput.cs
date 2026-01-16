namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record CompleteRefTestInput(
    string Token,
    List<string> SelectedAnswerIds,
    string? Language = null
);

