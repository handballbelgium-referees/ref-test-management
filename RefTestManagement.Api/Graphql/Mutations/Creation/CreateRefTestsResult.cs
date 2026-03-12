using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Permissions.AuditLog;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

public class CreateRefTestsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullyCreated { get; set; }
    public int Failed { get; set; }
    [AuditResultId]
    public List<RefTestDto> CreatedRefTests { get; set; } = [];
    public List<CreateRefTestsError> Errors { get; set; } = [];
}

public class CreateRefTestsError
{
    public User User { get; set; } = null!;
    public string ErrorMessage { get; set; } = string.Empty;
}



