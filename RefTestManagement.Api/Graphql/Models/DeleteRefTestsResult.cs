using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public class DeleteRefTestsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullyDeleted { get; set; }
    public int Failed { get; set; }
    public List<RefTest> DeletedRefTests { get; set; } = [];
    public List<DeleteRefTestError> Errors { get; set; } = [];
}

public class DeleteRefTestError
{
    public Guid RefTestId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}