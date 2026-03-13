using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.AuditLog;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;

public class ApproveRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyApproved { get; set; }
    public int Failed { get; set; }
    [AuditResultId] public List<RefTestDto> ApprovedRefTests { get; set; } = [];
    public List<ApprovalError> Errors { get; set; } = [];
}

public class RejectRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyRejected { get; set; }
    public int Failed { get; set; }
    [AuditResultId] public List<RefTestDto> RejectedRefTests { get; set; } = [];
    public List<ApprovalError> Errors { get; set; } = [];
}

public class ApprovalError
{
    public Guid RefTestId { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}
