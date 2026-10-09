namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>
/// Service for generating HTML email templates
/// </summary>
public interface IEmailTemplateService
{
    // Complete email builders - build the entire email including logo fetch
    Task<string> BuildCompleteInvitationEmailAsync(List<LanguageContent> enabledLanguages,
        string name, int numberOfQuestions, int maxTimeInMinutes);

    Task<string> BuildCompleteResultsEmailAsync(List<LanguageContent> enabledLanguages,
        string name, int questionScore, int answerScore, int totalQuestions, int answerTotal, double percentage,
        bool passed, string resultColor, string resultBgColor, string resultIcon, string enabledLanguagesDisplay);

    Task<string> BuildCompleteReportEmailAsync(List<LanguageContent> enabledLanguages,
        DateTime reportDate, int refTestCount);

    Task<string> BuildCompleteApprovalNotificationEmailAsync(
        List<LanguageContent> enabledLanguages,
        string creatorName,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
        string baseUrl);

    Task<string> BuildCompleteApprovalDecisionEmailAsync(
        List<LanguageContent> enabledLanguages,
        string approverName,
        bool isApproved,
        string? rejectionReason,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems);

    Task<string> BuildCompletePersonalDataExportVerificationEmailAsync(
        List<LanguageContent> enabledLanguages);

    Task<string> BuildCompletePrivacyWithdrawalVerificationEmailAsync(
        List<LanguageContent> enabledLanguages);

    Task<string> BuildCompletePersonalDataExportDeliveryEmailAsync(
        List<LanguageContent> enabledLanguages);
}

public record LanguageContent(
    string RefTestUrl,
    IReadOnlyDictionary<string, string> Translations
);
