namespace Handball.Belgium.RefTestManagement.Application.Models;

/// <summary>
/// Base interface for job payloads
/// </summary>
public interface IJobPayload
{
}

/// <summary>
/// Payload for invitation email jobs
/// </summary>
public record InvitationEmailPayload(
    Guid RefTestId,
    string Name,
    string Email,
    string Token,
    int NumberOfQuestions,
    int MaxTimeInMinutes
) : IJobPayload;

/// <summary>
/// Payload for result email jobs
/// </summary>
public record ResultEmailPayload(
    Guid RefTestId,
    string Name,
    string Email,
    int QuestionScore,
    int AnswerScore,
    int TotalQuestions,
    int AnswerTotal,
    double Percentage,
    List<string> SelectedAnswerIds,
    List<string> WrongQuestionIds,
    List<string> WrongAnswerIds
) : IJobPayload;

/// <summary>
/// Payload for report email jobs
/// </summary>
public record ReportEmailPayload(
    string[] RecipientEmails,
    List<RefTestReportPayloadData> RefTests,
    string Timestamp
) : IJobPayload;

/// <summary>
/// RefTest data for report email payload
/// </summary>
public record RefTestReportPayloadData(
    string TitleName,
    string FirstName,
    string LastName,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? QuestionScore,
    int QuestionTotal,
    int? AnswerScore,
    int? AnswerTotal,
    double? Percentage,
    bool Passed,
    string? Language,
    TimeSpan? Duration
);

/// <summary>
/// Payload for RefTest expiration check jobs
/// </summary>
public record RefTestExpirationPayload(
    Guid RefTestId,
    RefTestExpirationAction Action
) : IJobPayload;

/// <summary>
/// Payload for approval notification emails sent to approvers when RefTests are awaiting review
/// </summary>
public record ApprovalNotificationEmailPayload(
    string CreatorName,
    string CreatorEmail,
    string? TitleValue,
    List<ApprovalNotificationRefTestItem> RefTests
) : IJobPayload;

/// <summary>
/// Per-RefTest entry in an approval notification email
/// </summary>
public record ApprovalNotificationRefTestItem(
    Guid Id,
    string FirstName,
    string LastName,
    string Email
);

/// <summary>
/// Action to take for an expired RefTest
/// </summary>
public enum RefTestExpirationAction
{
    /// <summary>
    /// Mark the RefTest as expired (for pending tests)
    /// </summary>
    MarkAsExpired,
    
    /// <summary>
    /// Auto-complete the RefTest (for in-progress tests)
    /// </summary>
    AutoComplete
}

