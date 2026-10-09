using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IEmailService
{
    Task<bool> SendRefTestInvitationAsync(Guid refTestId, string name, string email, string token, int numberOfQuestions,
        int maxTimeInMinutes, CancellationToken cancellationToken);

    Task<bool> SendRefTestResultsAsync(Guid refTestId, string name, string email, int questionScore, int answerScore, int totalQuestions,
        int answerTotal, double percentage, List<string> selectedAnswerIds, List<string> wrongQuestionIds,
        List<string> wrongAnswerIds, List<Question> questionsWithCorrectAnswers, bool scheduleEmail,
        CancellationToken cancellationToken);

    Task SendReportEmailAsync(string recipientEmail, byte[] excelReport, byte[] pdfReport, string timestamp,
        int refTestCount, CancellationToken cancellationToken);

    Task SendApprovalNotificationAsync(
        string approverName,
        string approverEmail,
        string creatorName,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
        string baseUrl,
        CancellationToken cancellationToken);

    Task SendApprovalDecisionAsync(
        string creatorName,
        string creatorEmail,
        string approverName,
        bool isApproved,
        string? rejectionReason,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
        CancellationToken cancellationToken);

    Task<bool> SendPersonalDataExportVerificationAsync(
        string recipientEmail,
        string challengeKey,
        DateTime expiresAt,
        Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
        CancellationToken cancellationToken);

    Task<bool> SendPrivacyWithdrawalVerificationAsync(
        string recipientEmail,
        string challengeKey,
        DateTime expiresAt,
        Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
        CancellationToken cancellationToken);

    Task<bool> SendPersonalDataExportAsync(
        string recipientEmail,
        IReadOnlyList<EmailAttachment> attachments,
        Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
        CancellationToken cancellationToken);
}

public record EmailAttachment(string FileName, byte[] Content);
