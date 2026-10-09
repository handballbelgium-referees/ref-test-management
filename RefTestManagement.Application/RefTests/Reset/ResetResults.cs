using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Reset;

public sealed record ResetRefTestsError(Guid RefTestId, string ErrorMessage);

public sealed record ResetRefTestsResult(
    int TotalRequested,
    List<RefTest> ResetRefTests,
    List<ResetRefTestsError> Errors,
    int SuccessfullyReset);

public sealed record ReviveRefTestsError(Guid RefTestId, string ErrorMessage);

public sealed record ReviveRefTestsResult(
    int TotalRequested,
    List<RefTest> RevivedRefTests,
    List<ReviveRefTestsError> Errors,
    int SuccessfullyRevived);
