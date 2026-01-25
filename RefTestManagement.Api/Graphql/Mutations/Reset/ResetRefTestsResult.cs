using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;
public record ResetRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyReset { get; init; }
    public int Failed { get; init; }
    public List<RefTestDto> ResetRefTests { get; set; } = [];

    public List<ResetRefTestsError> Errors { get; init; } = [];
}
public record ResetRefTestsError
{
    public Guid RefTestId { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}
