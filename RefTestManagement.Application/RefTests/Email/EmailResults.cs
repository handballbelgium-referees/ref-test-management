using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Email;

public sealed class SendInvitationsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullySent { get; set; }
    public int Failed { get; set; }
    public List<RefTest> SentRefTests { get; } = [];
    public List<SendInvitationError> Errors { get; } = [];
}

public sealed record SendInvitationError(Guid RefTestId, RefTest? RefTest, string ErrorMessage);

public sealed class SendResultsResult
{
    public int TotalRequested { get; init; }
    public int SuccessfullySent { get; set; }
    public int Failed { get; set; }
    public List<RefTest> SentRefTests { get; } = [];
    public List<SendResultError> Errors { get; } = [];
}

public sealed record SendResultError(Guid RefTestId, RefTest? RefTest, string ErrorMessage);

public sealed record SendReportResult(bool Success, string Message, int RefTestCount);
