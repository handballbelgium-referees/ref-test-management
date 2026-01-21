using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public partial class EmailService(
    ILogger<EmailService> logger,
    EmailConfiguration configuration,
    LanguageConfiguration languageConfiguration,
    ScoreConfiguration scoreConfiguration,
    RefTestExpirationConfiguration refTestExpirationConfiguration,
    IRefTestResultsPdfService pdfService,
    IEmailTemplateService templateService,
    HttpClient httpClient)
    : IEmailService
{
    // Compiled regex for performance (allocated once)
    private static readonly Regex HtmlTagRegex = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    // Cache translation dictionaries (allocated once instead of per email)
    private Dictionary<string, Dictionary<string, string>>? _invitationTranslationsCache;
    private Dictionary<string, Dictionary<string, string>>? _resultsTranslationsCache;
    private Dictionary<string, Dictionary<string, string>>? _reportTranslationsCache;
    public async Task SendRefTestInvitationAsync(string name, string email, string token, int numberOfQuestions,
        int maxTimeInMinutes, CancellationToken cancellationToken)
    {
        var enabledLanguages = GetEnabledLanguagesForInvitation(token);

        const string subject = "Referees Handball Belgium RefTest - Invitation";
        var emailBody = await templateService.BuildCompleteInvitationEmailAsync(enabledLanguages, name, numberOfQuestions, maxTimeInMinutes);

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
        var enabledLanguagesDisplay = string.Join(", ", languageConfiguration.EnabledLanguages.Select(l => GetInvitationTranslations()[l]["displayName"]));

        var emailBody = await templateService.BuildCompleteResultsEmailAsync(enabledLanguages, name, questionScore, answerScore,
            totalQuestions, answerTotal, percentage, passed, resultColor, resultBgColor, resultIcon, enabledLanguagesDisplay);

        // Generate PDF attachments for enabled languages only
        var attachments = languageConfiguration.EnabledLanguages.Select(lang =>
        {
            var langUpper = lang.ToUpperInvariant();
            return new EmailAttachment($"RefTest_Results_{langUpper}.pdf",
                pdfService.GenerateRefTestResultsPdf(name, lang, questionScore, answerScore, totalQuestions,
                    answerTotal, percentage, selectedAnswerIds, wrongQuestionIds,
                    wrongAnswerIds, questionsWithCorrectAnswers));
        }).ToList();

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


    private Dictionary<string, Dictionary<string, string>> GetInvitationTranslations()
    {
        // Lazy initialization - create once and cache
        if (_invitationTranslationsCache != null)
            return _invitationTranslationsCache;

        var expiration = refTestExpirationConfiguration.ExpirationIfNotStarted;


        // Format the expiration time in a human-readable way for each language
        string GetValidityText(string lang)
        {
            var totalHours = (int)expiration.TotalHours;
            var totalDays = (int)expiration.TotalDays;

            return lang switch
            {
                "en" => totalHours < 24
                    ? $"⏰ This RefTest is valid for {totalHours} {(totalHours == 1 ? "hour" : "hours")}"
                    : $"⏰ This RefTest is valid for {totalDays} {(totalDays == 1 ? "day" : "days")}",
                "nl" => totalHours < 24
                    ? $"⏰ Deze RefTest is {totalHours} {(totalHours == 1 ? "uur" : "uren")} geldig"
                    : $"⏰ Deze RefTest is {totalDays} {(totalDays == 1 ? "dag" : "dagen")} geldig",
                "fr" => totalHours < 24
                    ? $"⏰ Ce RefTest est valide pendant {totalHours} {(totalHours <= 1 ? "heure" : "heures")}"
                    : $"⏰ Ce RefTest est valide pendant {totalDays} {(totalDays <= 1 ? "jour" : "jours")}",
                "de" => totalHours < 24
                    ? $"⏰ Dieser RefTest ist {totalHours} {(totalHours == 1 ? "Stunde" : "Stunden")} lang gültig"
                    : $"⏰ Dieser RefTest ist {totalDays} {(totalDays == 1 ? "Tag" : "Tage")} lang gültig",
                _ => $"⏰ Valid for {totalDays} days"
            };
        }

        _invitationTranslationsCache = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["displayName"] = "English",
                ["refTestDetails"] = "RefTest Details",
                ["questions"] = "Questions",
                ["timeLimit"] = "Time Limit",
                ["minutes"] = "minutes",
                ["validDays"] = GetValidityText("en"),
                ["greeting"] = "Hello",
                ["inviteText"] =
                    "You have been invited to take the RefTest. Click the button below to start your RefTest:",
                ["startButton"] = "Start RefTest"
            },
            ["nl"] = new()
            {
                ["displayName"] = "Nederlands",
                ["refTestDetails"] = "RefTest Details",
                ["questions"] = "Vragen",
                ["timeLimit"] = "Tijdslimiet",
                ["minutes"] = "minuten",
                ["validDays"] = GetValidityText("nl"),
                ["greeting"] = "Hallo",
                ["inviteText"] =
                    "Je bent uitgenodigd om deel te nemen aan de RefTest. Klik op de knop hieronder om je RefTest te starten:",
                ["startButton"] = "RefTest Starten"
            },
            ["fr"] = new()
            {
                ["displayName"] = "Français",
                ["refTestDetails"] = "Détails du RefTest",
                ["questions"] = "Questions",
                ["timeLimit"] = "Limite de Temps",
                ["minutes"] = "minutes",
                ["validDays"] = GetValidityText("fr"),
                ["greeting"] = "Bonjour",
                ["inviteText"] =
                    "Vous êtes invité à participer au RefTest. Cliquez sur le bouton ci-dessous pour commencer votre RefTest:",
                ["startButton"] = "Démarrer le RefTest"
            },
            ["de"] = new()
            {
                ["displayName"] = "Deutsch",
                ["refTestDetails"] = "RefTest-Details",
                ["questions"] = "Fragen",
                ["timeLimit"] = "Zeitlimit",
                ["minutes"] = "Minuten",
                ["validDays"] = GetValidityText("de"),
                ["greeting"] = "Hallo",
                ["inviteText"] =
                    "Sie wurden eingeladen, am RefTest teilzunehmen. Klicken Sie auf die Schaltfläche unten, um Ihren RefTest zu starten:",
                ["startButton"] = "RefTest starten"
            }
        };

        return _invitationTranslationsCache;
    }

    private Dictionary<string, Dictionary<string, string>> GetResultsTranslations()
    {
        // Lazy initialization - create once and cache
        if (_resultsTranslationsCache != null)
            return _resultsTranslationsCache;

        _resultsTranslationsCache = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["displayName"] = "English",
            ["passed"] = "PASSED",
            ["notPassed"] = "NOT PASSED",
            ["score"] = "Your Score",
            ["questions"] = "Q",
            ["answers"] = "A",
            ["percentage"] = "Percentage",
            ["passedMessage"] = "🎉 Congratulations! You passed the RefTest!",
            ["failedMessage"] = "📚 Keep studying and good luck next time!",
            ["greeting"] = "Dear",
            ["passedText"] =
                "Congratulations! You have successfully passed the RefTest! Your knowledge of handball regulations is excellent.",
            ["failedText"] =
                "Thank you for taking the RefTest. A passing score is 80% or higher. Please review the rules and try again.",
            ["pdfNote"] = "📎 <strong>Detailed results are available in the attached PDF documents</strong>"
        },
        ["nl"] = new Dictionary<string, string>
        {
            ["displayName"] = "Nederlands",
            ["passed"] = "GESLAAGD",
            ["notPassed"] = "NIET GESLAAGD",
            ["score"] = "Jouw Score",
            ["questions"] = "V",
            ["answers"] = "A",
            ["percentage"] = "Percentage",
            ["passedMessage"] = "🎉 Gefeliciteerd! Je bent geslaagd!",
            ["failedMessage"] = "📚 Blijf studeren en veel succes de volgende keer!",
            ["greeting"] = "Hallo",
            ["passedText"] =
                "Gefeliciteerd! Je bent geslaagd voor de RefTest! Je kennis van de handbalreglementen is uitstekend.",
            ["failedText"] =
                "Bedankt voor het maken van de RefTest. Een slaagpercentage is 80% of hoger. Bekijk de regels en probeer het opnieuw.",
            ["pdfNote"] =
                "📎 <strong>Gedetailleerde resultaten zijn beschikbaar in de bijgevoegde PDF-documenten</strong>"
        },
        ["fr"] = new Dictionary<string, string>
        {
            ["displayName"] = "Français",
            ["passed"] = "RÉUSSI",
            ["notPassed"] = "NON RÉUSSI",
            ["score"] = "Votre Score",
            ["questions"] = "Q",
            ["answers"] = "R",
            ["percentage"] = "Pourcentage",
            ["passedMessage"] = "🎉 Félicitations ! Vous avez réussi !",
            ["failedMessage"] = "📚 Continuez à étudier et bonne chance la prochaine fois !",
            ["greeting"] = "Bonjour",
            ["passedText"] =
                "Félicitations! Vous avez réussi le RefTest ! Votre connaissance des règles de handball est excellente.",
            ["failedText"] =
                "Merci d'avoir participé au RefTest. Un score de 80% ou plus est requis pour réussir. Veuillez réviser les règles et réessayer.",
            ["pdfNote"] = "📎 <strong>Les résultats détaillés sont disponibles dans les documents PDF joints</strong>"
        },
        ["de"] = new Dictionary<string, string>
        {
            ["displayName"] = "Deutsch",
            ["passed"] = "BESTANDEN",
            ["notPassed"] = "NICHT BESTANDEN",
            ["score"] = "Ihre Punktzahl",
            ["questions"] = "F",
            ["answers"] = "A",
            ["percentage"] = "Prozentsatz",
            ["passedMessage"] = "🎉 Herzlichen Glückwunsch! Sie haben bestanden!",
            ["failedMessage"] = "📚 Lernen Sie weiter und viel Glück beim nächsten Mal!",
            ["greeting"] = "Hallo",
            ["passedText"] =
                "Herzlichen Glückwunsch! Sie haben das RefTest bestanden! Ihre Kenntnisse der Handballregeln sind ausgezeichnet.",
            ["failedText"] =
                "Vielen Dank, dass Sie am RefTest teilgenommen haben. Eine Punktzahl von 80% oder höher ist erforderlich zum Bestehen. Bitte überprüfen Sie die Regeln und versuchen Sie es erneut.",
            ["pdfNote"] = "📎 <strong>Detaillierte Ergebnisse sind in den beigefügten PDF-Dokumenten verfügbar</strong>"
        }
    };

        return _resultsTranslationsCache;
    }

    private Dictionary<string, Dictionary<string, string>> GetReportTranslations()
    {
        // Lazy initialization - create once and cache
        if (_reportTranslationsCache != null)
            return _reportTranslationsCache;

        _reportTranslationsCache = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["displayName"] = "English",
            ["introText"] = "This is an automatically generated report containing RefTest data.",
            ["reportDate"] = "Report Date",
            ["numberOfRefTests"] = "Number of RefTests",
            ["attachmentText"] = "The report is attached in both Excel (.xlsx) and PDF formats.",
            ["reportDetails"] = "Report Details"
        },
        ["nl"] = new Dictionary<string, string>
        {
            ["displayName"] = "Nederlands",
            ["introText"] = "Dit is een automatisch gegenereerd rapport met RefTest-gegevens.",
            ["reportDate"] = "Rapportdatum",
            ["numberOfRefTests"] = "Aantal RefTests",
            ["attachmentText"] = "Het rapport is bijgevoegd in zowel Excel (.xlsx) als PDF-formaat.",
            ["reportDetails"] = "Rapportdetails"
        },
        ["fr"] = new Dictionary<string, string>
        {
            ["displayName"] = "Français",
            ["introText"] = "Ceci est un rapport généré automatiquement contenant des données de RefTest.",
            ["reportDate"] = "Date du Rapport",
            ["numberOfRefTests"] = "Nombre de RefTests",
            ["attachmentText"] = "Le rapport est joint aux formats Excel (.xlsx) et PDF.",
            ["reportDetails"] = "Détails du Rapport"
        },
        ["de"] = new Dictionary<string, string>
        {
            ["displayName"] = "Deutsch",
            ["introText"] = "Dies ist ein automatisch generierter Bericht mit RefTest-Daten.",
            ["reportDate"] = "Berichtsdatum",
            ["numberOfRefTests"] = "Anzahl der RefTests",
            ["attachmentText"] = "Der Bericht ist sowohl im Excel- (.xlsx) als auch im PDF-Format beigefügt.",
            ["reportDetails"] = "Berichtsdetails"
        }
    };

        return _reportTranslationsCache;
    }

    private List<LanguageContent> GetEnabledLanguagesForInvitation(string token)
    {
        var translations = GetInvitationTranslations();
        return
        [
            .. languageConfiguration.EnabledLanguages
                .Where(lang => translations.ContainsKey(lang))
                .Select(lang => new LanguageContent(
                    $"{configuration.BaseUrl}/ref-test/{token}?lang={lang}",
                    translations[lang]
                ))
        ];
    }

    private List<LanguageContent> GetEnabledLanguagesForResults()
    {
        var translations = GetResultsTranslations();
        return
        [
            .. languageConfiguration.EnabledLanguages
                .Where(lang => translations.ContainsKey(lang))
                .Select(lang => new LanguageContent(
                    string.Empty,
                    translations[lang]
                ))
        ];
    }

    private List<LanguageContent> GetEnabledLanguagesForReport()
    {
        var translations = GetReportTranslations();
        return
        [
            .. languageConfiguration.EnabledLanguages
                .Where(lang => translations.ContainsKey(lang))
                .Select(lang => new LanguageContent(
                    string.Empty,
                    translations[lang]
                ))
        ];
    }
}

public class EmailException(string email) : Exception($"An error occurred while sending the email to {email}");

public record EmailAttachment(string FileName, byte[] Content);

