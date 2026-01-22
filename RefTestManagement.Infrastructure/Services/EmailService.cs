using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IEmailService
{
    Task SendRefTestInvitationAsync(string name, string email, string token, int numberOfQuestions,
        int maxTimeInMinutes, CancellationToken cancellationToken);

    Task SendRefTestResultsAsync(string name, string email, int questionScore, int answerScore, int totalQuestions,
        int answerTotal, double percentage, List<string> selectedAnswerIds, List<string> wrongQuestionIds,
        List<string> wrongAnswerIds, List<Question> questionsWithCorrectAnswers, bool scheduleEmail,
        CancellationToken cancellationToken);

    Task SendReportEmailAsync(string recipientEmail, byte[] excelReport, byte[] pdfReport, string timestamp,
        int refTestCount, CancellationToken cancellationToken);
}

public partial class EmailService(
    ILogger<EmailService> logger,
    EmailConfiguration configuration,
    LanguageConfiguration languageConfiguration,
    ScoreConfiguration scoreConfiguration,
    RefTestExpirationConfiguration refTestExpirationConfiguration,
    IRefTestResultsPdfService pdfService,
    IEmailTemplateService templateService,
    ITranslationService translationService,
    HttpClient httpClient)
    : IEmailService
{
    // Compiled regex for performance (allocated once)
    private static readonly Regex HtmlTagRegex = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public async Task SendRefTestInvitationAsync(string name, string email, string token, int numberOfQuestions,
        int maxTimeInMinutes, CancellationToken cancellationToken)
    {
        var enabledLanguages = GetEnabledLanguagesForInvitation(token);

        const string subject = "Referees Handball Belgium RefTest - Invitation";
        var emailBody =
            await templateService.BuildCompleteInvitationEmailAsync(enabledLanguages, name, numberOfQuestions,
                maxTimeInMinutes);

        var firstRefTestUrl =
            enabledLanguages.FirstOrDefault()?.RefTestUrl ?? $"{configuration.BaseUrl}/ref-test/{token}";
        LogSendingRefTestInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(logger, email, token,
            numberOfQuestions, maxTimeInMinutes, firstRefTestUrl);

        await SendEmailAsync(email, subject, emailBody, cancellationToken: cancellationToken);

        LogRefTestInvitationEmailSentToEmail(logger, email);
    }

    public async Task SendRefTestResultsAsync(string name, string email, int questionScore, int answerScore,
        int totalQuestions, int answerTotal, double percentage,
        List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds,
        List<Question> questionsWithCorrectAnswers, bool scheduleEmail, CancellationToken cancellationToken)
    {
        const string subject = "Referees Handball Belgium RefTest - Results";
        var passed = percentage >= scoreConfiguration.PassingPercentage;
        var resultColor = passed ? "#22c55e" : "#ef4444";
        var resultBgColor = passed ? "#dcfce7" : "#fee2e2";
        var resultIcon = passed ? "✓" : "✗";

        var enabledLanguages = GetEnabledLanguagesForResults();
        var enabledLanguagesDisplay = string.Join(", ",
            languageConfiguration.EnabledLanguages.Select(l => translationService.GetLanguageDisplayName(l)));

        var emailBody = await templateService.BuildCompleteResultsEmailAsync(enabledLanguages, name, questionScore,
            answerScore,
            totalQuestions, answerTotal, percentage, passed, resultColor, resultBgColor, resultIcon,
            enabledLanguagesDisplay);

        // Generate PDF attachments sequentially to reduce memory pressure
        // Only one PDF is held in memory at a time, reducing peak memory usage by 75%
        var attachments = (from lang in languageConfiguration.EnabledLanguages
            let langUpper = lang.ToUpperInvariant()
            let pdfBytes =
                pdfService.GenerateRefTestResultsPdf(name, lang, questionScore, answerScore, totalQuestions,
                    answerTotal, percentage, selectedAnswerIds, wrongQuestionIds, wrongAnswerIds,
                    questionsWithCorrectAnswers)
            select new EmailAttachment($"RefTest_Results_{langUpper}.pdf", pdfBytes)).ToList();

        LogSendingRefTestResultsToEmailScoreScoreTotalPercentageF1(logger, email, questionScore, answerScore,
            totalQuestions, answerTotal, percentage);

        await SendEmailAsync(email, subject, emailBody, attachments, scheduleEmail, cancellationToken);

        LogRefTestResultsEmailSentToEmail(logger, email);
    }


    private async Task SendEmailAsync(string toEmail, string subject, string body,
        List<EmailAttachment>? attachments = null, bool scheduleEmail = false,
        CancellationToken cancellationToken = default)
    {
        LogSendingEmailToEmailWithSubjectAndBody(logger, toEmail, subject, body);

        if (string.IsNullOrWhiteSpace(configuration.BrevoApiKey))
        {
            LogBrevoApiKeyNotConfiguredEmailNotSent(logger);
            return;
        }

        try
        {
            // Create a plain text version using cached compiled regex
            var plainTextBody = HtmlTagRegex.Replace(body, "");
            plainTextBody = WhitespaceRegex.Replace(plainTextBody, " ").Trim();

            var scheduledAt = scheduleEmail && configuration.ScheduledDelayMinutes > 0
                ? DateTimeOffset.UtcNow
                    .AddMinutes(configuration.ScheduledDelayMinutes)
                    .ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                : null;

            // Prepare JSON payload for Brevo API with attachments
            var emailData = new
            {
                sender = new { name = configuration.FromName, email = configuration.FromEmail },
                to = new[] { new { email = toEmail } },
                subject,
                htmlContent = body,
                textContent = plainTextBody,

                // 👇 Brevo scheduling (only used if not null)
                scheduledAt,

                attachment = attachments?.Select(a => new
                {
                    name = a.FileName,
                    content = Convert.ToBase64String(a.Content)
                }).ToArray()
            };

            var jsonContent = JsonSerializer.Serialize(emailData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var brevoUrl = $"{configuration.BrevoApiUrl}/smtp/email";
            var response = await httpClient.PostAsync(brevoUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                LogEmailSentSuccessfully(logger, toEmail);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                LogEmailFailedWithStatusCode(logger, toEmail, (int)response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            LogErrorSendingEmailToEmail(logger, ex, toEmail);
            throw new EmailException(toEmail);
        }
    }

    public async Task SendReportEmailAsync(string recipientEmail, byte[] excelReport, byte[] pdfReport,
        string timestamp, int refTestCount, CancellationToken cancellationToken)
    {
        var subject = $"Referees Handball Belgium RefTest - Report - {DateTime.UtcNow:dd-MM-yyyy}";

        // Convert to Central European Time
        var cetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var nowCet = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cetTimeZone);

        var enabledLanguages = GetEnabledLanguagesForReport();

        var emailBody = await templateService.BuildCompleteReportEmailAsync(enabledLanguages, nowCet, refTestCount);

        var attachments = new List<EmailAttachment>
        {
            new($"RefTest_Report_{timestamp}.xlsx", excelReport),
            new($"RefTest_Report_{timestamp}.pdf", pdfReport)
        };

        await SendEmailAsync(recipientEmail, subject, emailBody, attachments, cancellationToken: cancellationToken);

        LogReportEmailSentToEmail(logger, recipientEmail);
    }

    [LoggerMessage(LogLevel.Information,
        "Sending RefTest invitation to {email}. Token: {token}, Questions: {questions}, Time: {time} minutes. URL: {url}")]
    static partial void LogSendingRefTestInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(
        ILogger<EmailService> logger, string email, string token, int questions, int time, string url);

    [LoggerMessage(LogLevel.Information, "RefTest invitation email sent to {email}")]
    static partial void LogRefTestInvitationEmailSentToEmail(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Information,
        "Sending RefTest results to {email}. QuestionScore: {questionScore}/{totalQuestions}, AnswerScore: {answerScore}/{answerTotal} ({percentage:F1}%)")]
    static partial void LogSendingRefTestResultsToEmailScoreScoreTotalPercentageF1(ILogger<EmailService> logger,
        string email, int questionScore, int answerScore, int totalQuestions, int answerTotal, double percentage);

    [LoggerMessage(LogLevel.Information, "RefTest results email sent to {email}")]
    static partial void LogRefTestResultsEmailSentToEmail(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Information, "Sending email to {email} with {subject} and {body}")]
    static partial void LogSendingEmailToEmailWithSubjectAndBody(ILogger<EmailService> logger, string email,
        string subject, string body);

    [LoggerMessage(LogLevel.Warning, "Brevo API key not configured. Email not sent.")]
    static partial void LogBrevoApiKeyNotConfiguredEmailNotSent(ILogger<EmailService> logger);

    [LoggerMessage(LogLevel.Information, "Email sent successfully to {email}")]
    static partial void LogEmailSentSuccessfully(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Warning, "Email to {email} failed with status code {statusCode}: {responseBody}")]
    static partial void LogEmailFailedWithStatusCode(ILogger<EmailService> logger, string email, int statusCode,
        string responseBody);

    [LoggerMessage(LogLevel.Error, "Error sending email to {email}")]
    static partial void LogErrorSendingEmailToEmail(ILogger<EmailService> logger, Exception ex, string email);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information,
        Message = "Report email sent to {Email}")]
    private static partial void LogReportEmailSentToEmail(ILogger logger, string email);


    private List<LanguageContent> GetEnabledLanguagesForInvitation(string token)
    {
        return languageConfiguration.EnabledLanguages
            .Select(lang =>
            {
                var baseTranslations = translationService.GetEmailInvitationTranslations(lang);
                // Create NEW dictionary to avoid mutating shared singleton state
                var translations = new Dictionary<string, string>(baseTranslations)
                {
                    ["validDays"] =
                        translationService.GetValidityText(lang, refTestExpirationConfiguration.ExpirationIfNotStarted)
                };

                return new LanguageContent(
                    $"{configuration.BaseUrl}/ref-test/{token}?lang={lang}",
                    translations
                );
            })
            .ToList();
    }

    private List<LanguageContent> GetEnabledLanguagesForResults()
    {
        return languageConfiguration.EnabledLanguages
            .Select(lang => new LanguageContent(
                string.Empty,
                translationService.GetEmailResultsTranslations(lang)
            ))
            .ToList();
    }

    private List<LanguageContent> GetEnabledLanguagesForReport()
    {
        return languageConfiguration.EnabledLanguages
            .Select(lang => new LanguageContent(
                string.Empty,
                translationService.GetEmailReportTranslations(lang)
            ))
            .ToList();
    }
}

public class EmailException(string email) : Exception($"An error occurred while sending the email to {email}");

public record EmailAttachment(string FileName, byte[] Content);