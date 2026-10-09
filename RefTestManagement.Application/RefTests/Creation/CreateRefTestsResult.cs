using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Creation;

public sealed class CreateRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyCreated { get; set; }
    public int Failed { get; set; }
    public List<RefTest> CreatedRefTests { get; } = [];
    public List<CreateRefTestsError> Errors { get; } = [];
}

public sealed record CreateRefTestsError(CreateRefTestUser User, string ErrorMessage);
