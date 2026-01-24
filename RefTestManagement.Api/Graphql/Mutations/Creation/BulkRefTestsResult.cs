using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

public class BulkRefTestsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullyCreated { get; set; }
    public int Failed { get; set; }
    public List<RefTestDto> CreatedRefTests { get; set; } = [];
    public List<BulkCreationError> Errors { get; set; } = [];
}

public class BulkCreationError
{
    public User User { get; set; } = null!;
    public string ErrorMessage { get; set; } = string.Empty;
}



