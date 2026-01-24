namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

public record ReviveRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyRevived { get; init; }
    public int Failed { get; init; }
    public List<ReviveRefTestsError> Errors { get; init; } = [];
}

public record ReviveRefTestsError
{
    public Guid RefTestId { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}
