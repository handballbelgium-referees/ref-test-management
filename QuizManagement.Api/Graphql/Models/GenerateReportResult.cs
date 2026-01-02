namespace QuizManagement.Api.Graphql.Models;

public record GenerateReportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int SessionCount { get; init; }
}

