namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;

public record GenerateReportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int RefTestCount { get; init; }
}



