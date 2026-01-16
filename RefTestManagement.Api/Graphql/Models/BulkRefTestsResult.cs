using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public class BulkRefTestsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullyCreated { get; set; }
    public int Failed { get; set; }
    public List<RefTest> CreatedRefTests { get; set; } = [];
    public List<BulkCreationError> Errors { get; set; } = [];
}

public class BulkCreationError
{
    public User User { get; set; } = null!;
    public string ErrorMessage { get; set; } = string.Empty;
}

