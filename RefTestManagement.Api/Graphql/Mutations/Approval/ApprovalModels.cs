using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;

public record ApproveRefTestsInput([property: ID<RefTestDto>] List<Guid> Ids);

public record ApproveRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyApproved { get; init; }
    public int Failed { get; init; }
    public List<RefTestDto> ApprovedRefTests { get; init; } = [];
    public List<ApproveRefTestsError> Errors { get; init; } = [];
}

public record ApproveRefTestsError
{
    public Guid RefTestId { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

public record RejectRefTestsInput([property: ID<RefTestDto>] List<Guid> Ids, string Reason);

public record RejectRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyRejected { get; init; }
    public int Failed { get; init; }
    public List<RefTestDto> RejectedRefTests { get; init; } = [];
    public List<RejectRefTestsError> Errors { get; init; } = [];
}

public record RejectRefTestsError
{
    public Guid RefTestId { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}
