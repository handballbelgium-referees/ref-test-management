using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Approval;

public sealed record ApproveRefTestsError(Guid RefTestId, string ErrorMessage);

public sealed record ApproveRefTestsResult(
    int TotalRequested,
    List<RefTest> ApprovedRefTests,
    List<ApproveRefTestsError> Errors);

public sealed record RejectRefTestsError(Guid RefTestId, string ErrorMessage);

public sealed record RejectRefTestsResult(
    int TotalRequested,
    List<RefTest> RejectedRefTests,
    List<RejectRefTestsError> Errors);
