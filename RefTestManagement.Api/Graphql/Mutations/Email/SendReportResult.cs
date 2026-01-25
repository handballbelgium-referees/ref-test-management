namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;
public record SendReportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int RefTestCount { get; init; }
}
