using Handball.Belgium.RefTestManagement.Infrastructure.Services;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Every user-visible text must exist in all four locales. A key present in English but missing in
/// another language falls back silently or throws at render time, so compare the key sets here.
/// </summary>
public sealed class TranslationParityTests
{
    private static readonly string[] Locales = ["nl", "fr", "de"];

    public static TheoryData<string> TranslationSets() =>
    [
        "EmailInvitation", "EmailResults", "EmailReport", "EmailApprovalNotification",
        "EmailApprovalApproved", "EmailApprovalRejected", "EmailPersonalDataExportVerification",
        "EmailPersonalDataExportDelivery", "EmailPrivacyWithdrawalVerification", "PdfResults",
        "PdfPersonalDataExport", "PdfReport", "ReportColumns"
    ];

    private static IReadOnlyDictionary<string, string> Get(TranslationService service, string set, string language) =>
        set switch
        {
            "EmailInvitation" => service.GetEmailInvitationTranslations(language),
            "EmailResults" => service.GetEmailResultsTranslations(language),
            "EmailReport" => service.GetEmailReportTranslations(language),
            "EmailApprovalNotification" => service.GetEmailApprovalNotificationTranslations(language),
            "EmailApprovalApproved" => service.GetEmailApprovalDecisionTranslations(language, isApproved: true),
            "EmailApprovalRejected" => service.GetEmailApprovalDecisionTranslations(language, isApproved: false),
            "EmailPersonalDataExportVerification" => service.GetEmailPersonalDataExportVerificationTranslations(language),
            "EmailPersonalDataExportDelivery" => service.GetEmailPersonalDataExportDeliveryTranslations(language),
            "EmailPrivacyWithdrawalVerification" => service.GetEmailPrivacyWithdrawalVerificationTranslations(language),
            "PdfResults" => service.GetPdfResultsTranslations(language),
            "PdfPersonalDataExport" => service.GetPdfPersonalDataExportTranslations(language),
            "PdfReport" => service.GetPdfReportTranslations(language),
            "ReportColumns" => service.GetReportColumnTranslations(language),
            _ => throw new ArgumentOutOfRangeException(nameof(set), set, null)
        };

    [Theory]
    [MemberData(nameof(TranslationSets))]
    public void EveryLocaleHasTheEnglishKeysAndNoOthers(string set)
    {
        var service = new TranslationService();
        var english = Get(service, set, "en").Keys.Order().ToArray();
        Assert.NotEmpty(english);

        foreach (var locale in Locales)
        {
            var translations = Get(service, set, locale);
            Assert.Equal(english, translations.Keys.Order());
            Assert.All(translations, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value),
                $"{set}/{locale}/{entry.Key} is empty"));
        }
    }
}
