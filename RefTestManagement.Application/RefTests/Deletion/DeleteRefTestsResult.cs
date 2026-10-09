using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Deletion;

public sealed class DeleteRefTestsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullyDeleted { get; set; }
    public int Failed { get; set; }
    public List<DeletedRefTestSnapshot> DeletedRefTests { get; } = [];
    public List<DeleteRefTestError> Errors { get; } = [];
}

public sealed record DeleteRefTestError(Guid RefTestId, string ErrorMessage);

/// <summary>Values required to return the deleted RefTest before its personal data was erased.</summary>
public sealed record DeletedRefTestSnapshot(
    RefTest RefTest,
    string FirstName,
    string LastName,
    string Email,
    string? Token,
    string? RejectionReason,
    bool IsAnonymized,
    DateTime? AnonymizedAt)
{
    public static DeletedRefTestSnapshot Capture(RefTest refTest) =>
        new(refTest, refTest.FirstName, refTest.LastName, refTest.Email, refTest.IssuedToken,
            refTest.RejectionReason, refTest.IsAnonymized, refTest.AnonymizedAt);
}
