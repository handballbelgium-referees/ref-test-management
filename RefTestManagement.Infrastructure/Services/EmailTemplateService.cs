using System.Text;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

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
}

public record LanguageContent(
    string RefTestUrl,
    IReadOnlyDictionary<string, string> Translations
);

public class EmailTemplateService(ILogoService logoService) : IEmailTemplateService
{
    // Cache the base templates (allocated once)
    private static readonly string InvitationBaseTemplate = BuildInvitationBaseTemplate();
    private static readonly string ResultsBaseTemplate = BuildResultsBaseTemplate();
    private static readonly string ReportBaseTemplate = BuildReportBaseTemplate();
    private static readonly string ApprovalBaseTemplate = BuildApprovalBaseTemplate();
    private static readonly string ApprovalDecisionBaseTemplate = BuildApprovalDecisionBaseTemplate();

    // Complete email builders - handle logo fetch and entire email generation
    public async Task<string> BuildCompleteInvitationEmailAsync(
        List<LanguageContent> enabledLanguages, string name, int numberOfQuestions, int maxTimeInMinutes)
    {
        var logoTag = await CreateLogoImageTag();

        var languageSections =
            BuildAllInvitationLanguageSections(enabledLanguages, name, numberOfQuestions, maxTimeInMinutes);
        return BuildInvitationEmail(logoTag, languageSections);
    }

    public async Task<string> BuildCompleteResultsEmailAsync(
        List<LanguageContent> enabledLanguages, string name, int questionScore, int answerScore,
        int totalQuestions, int answerTotal, double percentage, bool passed, string resultColor,
        string resultBgColor, string resultIcon, string enabledLanguagesDisplay)
    {
        var logoTag = await CreateLogoImageTag();

        var languageSections = BuildAllResultsLanguageSections(enabledLanguages, name, questionScore, answerScore,
            totalQuestions, answerTotal, percentage, passed, resultColor, resultBgColor, resultIcon,
            enabledLanguagesDisplay);
        return BuildResultsEmail(logoTag, languageSections);
    }

    public async Task<string> BuildCompleteReportEmailAsync(
        List<LanguageContent> enabledLanguages, DateTime reportDate, int refTestCount)
    {
        var logoTag = await CreateLogoImageTag();

        var languageSections = BuildAllReportLanguageSections(enabledLanguages, reportDate, refTestCount);
        return BuildReportEmail(logoTag, languageSections);
    }

    public async Task<string> BuildCompleteApprovalNotificationEmailAsync(
        List<LanguageContent> enabledLanguages,
        string creatorName,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
        string baseUrl)
    {
        var logoTag = await CreateLogoImageTag();
        var languageSections = BuildAllApprovalLanguageSections(
            enabledLanguages, creatorName, titleValue, refTestItems, baseUrl);
        return BuildApprovalEmail(logoTag, languageSections);
    }

    public async Task<string> BuildCompleteApprovalDecisionEmailAsync(
        List<LanguageContent> enabledLanguages,
        string approverName,
        bool isApproved,
        string? rejectionReason,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems)
    {
        var logoTag = await CreateLogoImageTag();
        var languageSections = BuildAllApprovalDecisionLanguageSections(
            enabledLanguages, approverName, isApproved, rejectionReason, titleValue, refTestItems);
        return BuildApprovalDecisionEmail(logoTag, languageSections);
    }

    private async Task<string> CreateLogoImageTag()
    {
        var logoBase64 = await logoService.GetLogoAsBase64Async();
        var logoTag = string.IsNullOrEmpty(logoBase64)
            ? ""
            : $"<img src='data:image/png;base64,{logoBase64}' alt='RefTest Logo' style='width: 100px; height: auto; margin-bottom: 10px;' />";
        return logoTag;
    }

    private static string BuildApprovalDecisionEmail(string logoTag, string languageSections)
    {
        return ApprovalDecisionBaseTemplate
            .Replace("{{LOGO}}", logoTag)
            .Replace("{{LANGUAGE_SECTIONS}}", languageSections);
    }

    private static string BuildApprovalEmail(string logoTag, string languageSections)
    {
        return ApprovalBaseTemplate
            .Replace("{{LOGO}}", logoTag)
            .Replace("{{LANGUAGE_SECTIONS}}", languageSections);
    }

    private static string BuildInvitationEmail(string logoTag, string languageSections)
    {
        return InvitationBaseTemplate
            .Replace("{{LOGO}}", logoTag)
            .Replace("{{LANGUAGE_SECTIONS}}", languageSections);
    }

    private static string BuildResultsEmail(string logoTag, string languageSections)
    {
        return ResultsBaseTemplate
            .Replace("{{LOGO}}", logoTag)
            .Replace("{{LANGUAGE_SECTIONS}}", languageSections);
    }

    private static string BuildReportEmail(string logoTag, string languageSections)
    {
        return ReportBaseTemplate
            .Replace("{{LOGO}}", logoTag)
            .Replace("{{LANGUAGE_SECTIONS}}", languageSections);
    }

    // High-level methods: Build all language sections at once
    private static string BuildAllInvitationLanguageSections(List<LanguageContent> enabledLanguages, string name,
        int numberOfQuestions, int maxTimeInMinutes)
    {
        var languageSections = new StringBuilder();

        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildInvitationLanguageSection(langContent, name, numberOfQuestions,
                maxTimeInMinutes, isLast));
        }

        return languageSections.ToString();
    }

    private static string BuildAllResultsLanguageSections(List<LanguageContent> enabledLanguages, string name,
        int questionScore, int answerScore,
        int totalQuestions, int answerTotal, double percentage, bool passed, string resultColor, string resultBgColor,
        string resultIcon, string enabledLanguagesDisplay)
    {
        var languageSections = new StringBuilder();

        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildResultsLanguageSection(langContent, name, questionScore, answerScore,
                totalQuestions, answerTotal, percentage, passed, resultColor, resultBgColor, resultIcon, isLast,
                enabledLanguagesDisplay));
        }

        return languageSections.ToString();
    }

    private static string BuildAllReportLanguageSections(List<LanguageContent> enabledLanguages, DateTime reportDate,
        int refTestCount)
    {
        var languageSections = new StringBuilder();

        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildReportLanguageSection(langContent, reportDate, refTestCount, isLast));
        }

        return languageSections.ToString();
    }

    private static string BuildAllApprovalLanguageSections(
        List<LanguageContent> enabledLanguages,
        string creatorName,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
        string baseUrl)
    {
        var languageSections = new StringBuilder();
        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildApprovalLanguageSection(
                langContent.Translations, creatorName, titleValue, refTestItems, baseUrl, isLast));
        }

        return languageSections.ToString();
    }

    private static string BuildApprovalLanguageSection(
        IReadOnlyDictionary<string, string> t,
        string creatorName,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
        string baseUrl,
        bool isLast)
    {
        var reviewUrl = $"{baseUrl.TrimEnd('/')}/ref-tests?status=PENDING_APPROVAL";

        var rows = new StringBuilder();
        foreach (var item in refTestItems)
        {
            var scheduledAtDisplay = item.ScheduledAt.HasValue
                ? item.ScheduledAt.Value.ToString("dd/MM/yyyy HH:mm") + " UTC"
                : "-";
            rows.Append($@"
                    <tr>
                        <td style='padding: 8px 12px; border-bottom: 1px solid #e5e7eb;'>{System.Web.HttpUtility.HtmlEncode(item.FullName)}</td>
                        <td style='padding: 8px 12px; border-bottom: 1px solid #e5e7eb;'>{System.Web.HttpUtility.HtmlEncode(item.Email)}</td>
                        <td style='padding: 8px 12px; border-bottom: 1px solid #e5e7eb;'>{System.Web.HttpUtility.HtmlEncode(scheduledAtDisplay)}</td>
                    </tr>");
        }

        var titleRow = string.IsNullOrWhiteSpace(titleValue)
            ? string.Empty
            : $"<p style='margin: 4px 0; color: #374151;'><strong>{System.Web.HttpUtility.HtmlEncode(t["title"])}:</strong> {System.Web.HttpUtility.HtmlEncode(titleValue)}</p>";

        var separator = isLast
            ? ""
            : @"
            <!-- Separator -->
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />";

        return $@"
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='margin: 0 0 16px 0; color: #111827; font-size: 22px;'>{System.Web.HttpUtility.HtmlEncode(t["heading"])}</h2>
                <p style='margin: 0 0 8px 0; color: #374151;'>{System.Web.HttpUtility.HtmlEncode(t["introText"])}</p>
                <p style='margin: 4px 0; color: #374151;'><strong>{System.Web.HttpUtility.HtmlEncode(t["createdBy"])}:</strong> {System.Web.HttpUtility.HtmlEncode(creatorName)}</p>
                {titleRow}
                <table style='width: 100%; border-collapse: collapse; margin: 20px 0; font-size: 14px;'>
                    <thead>
                        <tr style='background-color: #f9fafb;'>
                            <th style='padding: 10px 12px; text-align: left; border-bottom: 2px solid #e5e7eb; color: #374151;'>{System.Web.HttpUtility.HtmlEncode(t["tableNameHeader"])}</th>
                            <th style='padding: 10px 12px; text-align: left; border-bottom: 2px solid #e5e7eb; color: #374151;'>{System.Web.HttpUtility.HtmlEncode(t["tableEmailHeader"])}</th>
                            <th style='padding: 10px 12px; text-align: left; border-bottom: 2px solid #e5e7eb; color: #374151;'>{System.Web.HttpUtility.HtmlEncode(t["tableScheduledAtHeader"])}</th>
                        </tr>
                    </thead>
                    <tbody>{rows}</tbody>
                </table>
                <div style='text-align: center; margin: 24px 0;'>
                    <a href='{reviewUrl}' style='display: inline-block; background-color: #b30510; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 15px;'>{System.Web.HttpUtility.HtmlEncode(t["reviewButton"])}</a>
                </div>
                <div style='text-align: center; color: #737373; font-size: 13px; padding: 12px 0;'>
                    <p style='margin: 0;'>{System.Web.HttpUtility.HtmlEncode(t["footerNote"])}</p>
                </div>
            </div>{separator}";
    }

    private static string BuildInvitationBaseTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body { font-family: ui-sans-serif, system-ui, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji'; }
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <!-- Header with Belgian Handball Colors -->
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            {{LOGO}}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium RefTest - Invitation</p>
        </div>

        <!-- Main Content -->
        <div style='padding: 20px;'>
{{LANGUAGE_SECTIONS}}
            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private static string BuildResultsBaseTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body { font-family: ui-sans-serif, system-ui, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji'; }
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <!-- Header with Belgian Handball Colors -->
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            {{LOGO}}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium RefTest - Results</p>
        </div>

        <!-- Main Content -->
        <div style='padding: 20px;'>
{{LANGUAGE_SECTIONS}}
            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private static string BuildReportBaseTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body { font-family: ui-sans-serif, system-ui, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji'; }
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <!-- Header with Belgian Handball Colors -->
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            {{LOGO}}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium RefTest - Report</p>
        </div>

        <!-- Main Content -->
        <div style='padding: 20px;'>
{{LANGUAGE_SECTIONS}}
            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private static string BuildApprovalBaseTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body { font-family: ui-sans-serif, system-ui, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji'; }
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <!-- Header with Belgian Handball Colors -->
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            {{LOGO}}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium</p>
        </div>

        <!-- Main Content -->
        <div style='padding: 24px;'>
{{LANGUAGE_SECTIONS}}
            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private static string BuildApprovalDecisionBaseTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body { font-family: ui-sans-serif, system-ui, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji'; }
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            {{LOGO}}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium</p>
        </div>
        <div style='padding: 24px;'>
{{LANGUAGE_SECTIONS}}
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private static string BuildAllApprovalDecisionLanguageSections(
        List<LanguageContent> enabledLanguages,
        string approverName,
        bool isApproved,
        string? rejectionReason,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var isLast = i == enabledLanguages.Count - 1;
            sb.Append(BuildApprovalDecisionLanguageSection(
                enabledLanguages[i].Translations, approverName, isApproved,
                rejectionReason, titleValue, refTestItems, isLast));
        }
        return sb.ToString();
    }

    private static string BuildApprovalDecisionLanguageSection(
        IReadOnlyDictionary<string, string> t,
        string approverName,
        bool isApproved,
        string? rejectionReason,
        string? titleValue,
        List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
        bool isLast)
    {
        var accentColor = isApproved ? "#16a34a" : "#dc2626";
        var decisionKey = isApproved ? "approvedBy" : "rejectedBy";

        var rows = new StringBuilder();
        foreach (var item in refTestItems)
        {
            rows.Append($@"
                    <tr>
                        <td style='padding: 8px 12px; border-bottom: 1px solid #e5e7eb;'>{System.Web.HttpUtility.HtmlEncode(item.FullName)}</td>
                        <td style='padding: 8px 12px; border-bottom: 1px solid #e5e7eb;'>{System.Web.HttpUtility.HtmlEncode(item.Email)}</td>
                    </tr>");
        }

        var titleRow = string.IsNullOrWhiteSpace(titleValue)
            ? string.Empty
            : $"<p style='margin: 4px 0; color: #374151;'><strong>Title:</strong> {System.Web.HttpUtility.HtmlEncode(titleValue)}</p>";

        var reasonRow = !isApproved && !string.IsNullOrWhiteSpace(rejectionReason)
            ? $@"<div style='background-color: #fef2f2; border-left: 4px solid #dc2626; padding: 12px 16px; margin: 16px 0; border-radius: 4px;'>
                    <p style='margin: 0; color: #374151;'><strong>{System.Web.HttpUtility.HtmlEncode(t["reason"])}:</strong> {System.Web.HttpUtility.HtmlEncode(rejectionReason)}</p>
                 </div>"
            : string.Empty;

        var separator = isLast
            ? ""
            : @"
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />";

        return $@"
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='margin: 0 0 16px 0; color: {accentColor}; font-size: 22px;'>{System.Web.HttpUtility.HtmlEncode(t["heading"])}</h2>
                <p style='margin: 0 0 8px 0; color: #374151;'>{System.Web.HttpUtility.HtmlEncode(t["introText"])}</p>
                <p style='margin: 4px 0; color: #374151;'><strong>{System.Web.HttpUtility.HtmlEncode(t[decisionKey])}:</strong> {System.Web.HttpUtility.HtmlEncode(approverName)}</p>
                {titleRow}
                {reasonRow}
                <table style='width: 100%; border-collapse: collapse; margin: 20px 0; font-size: 14px;'>
                    <thead>
                        <tr style='background-color: #f9fafb;'>
                            <th style='padding: 10px 12px; text-align: left; border-bottom: 2px solid #e5e7eb; color: #374151;'>{System.Web.HttpUtility.HtmlEncode(t["tableNameHeader"])}</th>
                            <th style='padding: 10px 12px; text-align: left; border-bottom: 2px solid #e5e7eb; color: #374151;'>{System.Web.HttpUtility.HtmlEncode(t["tableEmailHeader"])}</th>
                        </tr>
                    </thead>
                    <tbody>{rows}</tbody>
                </table>
                <div style='text-align: center; color: #737373; font-size: 13px; padding: 12px 0;'>
                    <p style='margin: 0;'>{System.Web.HttpUtility.HtmlEncode(t["footerNote"])}</p>
                </div>
            </div>{separator}";
    }

    private static string BuildInvitationLanguageSection(LanguageContent langContent, string name,
        int numberOfQuestions,
        int maxTimeInMinutes, bool isLast)
    {
        var t = langContent.Translations;
        var separator = isLast
            ? ""
            : @"
            <!-- Separator -->
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />";

        return $@"
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>{t["displayName"]}</h2>
                
                <!-- Greeting -->
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>{t["greeting"]} {name},</p>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>
                    {t["inviteText"]}
                </p>

                <!-- RefTest Details Card -->
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin: 20px 0; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📋 {t["refTestDetails"]}</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["questions"]}:</strong> {numberOfQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["timeLimit"]}:</strong> {maxTimeInMinutes} {t["minutes"]}</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>{t["validDays"]}</em></p>
                </div>
                
                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{langContent.RefTestUrl}' style='background-color: #e30613; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;'>{t["startButton"]}</a>
                </div>
            </div>{separator}";
    }

    private static string BuildResultsLanguageSection(LanguageContent langContent, string name, int questionScore,
        int answerScore, int totalQuestions, int answerTotal,
        double percentage, bool passed, string resultColor, string resultBgColor, string resultIcon, bool isLast,
        string enabledLanguagesDisplay)
    {
        var t = langContent.Translations;
        var separator = isLast
            ? ""
            : @"
            <!-- Separator -->
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />";

        return $@"
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>{t["displayName"]}</h2>
                
                <!-- Score Display -->
                <div style='background-color: {resultBgColor}; border-left: 4px solid {resultColor}; padding: 20px; margin-bottom: 20px; border-radius: 4px;'>
                    <h3 style='color: {resultColor}; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>{resultIcon} {(passed ? t["passed"] : t["notPassed"])}</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["percentage"]}:</strong> {percentage:F2} %</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["score"]}:</strong> {t["questions"]}: {questionScore} / {totalQuestions} &nbsp; &nbsp; &nbsp;  {t["answers"]}: {answerScore} / {answerTotal}</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>{(passed ? t["passedMessage"] : t["failedMessage"])}</em></p>
                </div>

                <!-- Greeting -->
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>{t["greeting"]} {name},</p>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>
                    {(passed ? t["passedText"] : t["failedText"])}
                </p>

                <!-- PDF Reference -->
                <div style='background-color: #f5f5f5; border-left: 4px solid #e30613; padding: 16px; margin: 16px 0; border-radius: 4px;'>
                    <p style='margin: 0; color: #404040; font-size: 14px;'>{t["pdfNote"]} ({enabledLanguagesDisplay}).</p>
                </div>
            </div>{separator}";
    }

    private static string BuildReportLanguageSection(LanguageContent langContent, DateTime reportDate, int refTestCount,
        bool isLast)
    {
        var t = langContent.Translations;
        var separator = isLast
            ? ""
            : @"
            <!-- Separator -->
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />";

        return $@"
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>{t["displayName"]}</h2>
                
                <p style='font-size: 16px; line-height: 1.6; color: #374151;'>
                    {t["introText"]}
                </p>
                
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin: 20px 0; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📊 {t["reportDetails"]}</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["reportDate"]}:</strong> {reportDate:dd-MM-yyyy HH:mm}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["numberOfRefTests"]}:</strong> {refTestCount}</p>
                </div>

                <p style='font-size: 14px; color: #374151;'>
                    {t["attachmentText"]}
                </p>
            </div>{separator}";
    }
}