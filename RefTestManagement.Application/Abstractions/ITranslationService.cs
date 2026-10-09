namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>
/// Service for managing all application translations
/// </summary>
public interface ITranslationService
{
    IReadOnlyDictionary<string, string> GetEmailInvitationTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailResultsTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailReportTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailApprovalNotificationTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailApprovalDecisionTranslations(string language, bool isApproved);
    IReadOnlyDictionary<string, string> GetEmailPersonalDataExportVerificationTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailPersonalDataExportDeliveryTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailPrivacyWithdrawalVerificationTranslations(string language);
    IReadOnlyDictionary<string, string> GetPdfResultsTranslations(string language);
    IReadOnlyDictionary<string, string> GetPdfPersonalDataExportTranslations(string language);
    IReadOnlyDictionary<string, string> GetPdfReportTranslations(string language);
    IReadOnlyDictionary<string, string> GetReportColumnTranslations(string language);
    string GetLanguageDisplayName(string languageCode);
    string GetValidityText(string language, TimeSpan expiration);
}
