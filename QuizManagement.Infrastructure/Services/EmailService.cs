using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuizManagement.Application.Models;
using QuizManagement.Application.Services;

namespace QuizManagement.Infrastructure.Services;

public partial class EmailService(
    ILogger<EmailService> logger,
    EmailConfiguration configuration,
    LanguageConfiguration languageConfiguration,
    IQuizResultsPdfService pdfService)
    : IEmailService
{
    public async Task SendQuizInvitationAsync(string name, string email, string token, int numberOfQuestions,
        int maxTimeInMinutes)
    {
        var enabledLanguages = GetEnabledLanguagesForInvitation(token);
        var languageSections = new StringBuilder();

        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildInvitationLanguageSection(langContent, name, numberOfQuestions,
                maxTimeInMinutes, isLast));
        }

        const string subject = "Referees Handball Belgium RefTest Invitation";
        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: ui-sans-serif, system-ui, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji'; }}
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <!-- Header with Belgian Handball Colors -->
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>IHF Rules Quiz</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium RefTest Invitation</p>
        </div>

        <!-- Main Content -->
        <div style='padding: 20px;'>
{languageSections}
            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";


        var firstQuizUrl = enabledLanguages.FirstOrDefault()?.QuizUrl ?? $"{configuration.BaseUrl}/quiz/{token}";
        LogSendingQuizInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(logger, email, token,
            numberOfQuestions, maxTimeInMinutes, firstQuizUrl);

        await SendEmailAsync(email, subject, emailBody);

        LogQuizInvitationEmailSentToEmail(logger, email);
    }

    public async Task SendQuizResultsAsync(string name, string email, int score, int totalQuestions, double percentage,
        List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds,
        List<Question> questionsWithCorrectAnswers)
    {
        const string subject = "IHF Rules RefTest - Your Results";
        var passed = percentage >= 80;
        var resultColor = passed ? "#22c55e" : "#ef4444";
        var resultBgColor = passed ? "#dcfce7" : "#fee2e2";
        var resultIcon = passed ? "✓" : "✗";

        var enabledLanguages = GetEnabledLanguagesForResults();
        var languageSections = new StringBuilder();

        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildResultsLanguageSection(langContent, name, score, totalQuestions, percentage,
                passed, resultColor, resultBgColor, resultIcon, isLast));
        }

        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: ui-sans-serif, system-ui, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji'; }}
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <!-- Header with Belgian Handball Colors -->
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>IHF Rules RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Your Results</p>
        </div>

        <!-- Main Content -->
        <div style='padding: 20px;'>
{languageSections}
            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";


        // Generate PDF attachments for enabled languages only
        var attachments = languageConfiguration.EnabledLanguages.Select(lang =>
        {
            var langUpper = lang.ToUpperInvariant();
            return new EmailAttachment($"IHF_Rules_RefTest_Results_{langUpper}.pdf",
                pdfService.GenerateQuizResultsPdf(name, lang, totalQuestions, selectedAnswerIds, wrongQuestionIds,
                    wrongAnswerIds, questionsWithCorrectAnswers));
        }).ToList();

        LogSendingQuizResultsToEmailScoreScoreTotalPercentageF1(logger, email, score, totalQuestions, percentage);

        await SendEmailAsync(email, subject, emailBody, attachments, true);

        LogQuizResultsEmailSentToEmail(logger, email);
    }


    private async Task SendEmailAsync(string toEmail, string subject, string body,
        List<EmailAttachment>? attachments = null, bool scheduleEmail = false)
    {
        LogSendingEmailToEmailWithSubjectAndBody(logger, toEmail, subject, body);

        if (string.IsNullOrWhiteSpace(configuration.BrevoApiKey))
        {
            LogBrevoApiKeyNotConfiguredEmailNotSent(logger);
            return;
        }

        try
        {
            using var httpClient = new HttpClient();

            // Set up API key authentication for Brevo
            httpClient.DefaultRequestHeaders.Add("api-key", configuration.BrevoApiKey);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Create a plain text version by stripping HTML tags (simple version)
            var plainTextBody = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]*>", "");
            plainTextBody = System.Text.RegularExpressions.Regex.Replace(plainTextBody, @"\s+", " ").Trim();

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
            var response = await httpClient.PostAsync(brevoUrl, content);

            if (response.IsSuccessStatusCode)
            {
                LogEmailSentSuccessfully(logger, toEmail);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                LogEmailFailedWithStatusCode(logger, toEmail, (int)response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            LogErrorSendingEmailToEmail(logger, ex, toEmail);
            throw new EmailException(toEmail);
        }
    }

    [LoggerMessage(LogLevel.Information,
        "Sending quiz invitation to {email}. Token: {token}, Questions: {questions}, Time: {time} minutes. URL: {url}")]
    static partial void LogSendingQuizInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(
        ILogger<EmailService> logger, string email, string token, int questions, int time, string url);

    [LoggerMessage(LogLevel.Information, "Quiz invitation email sent to {email}")]
    static partial void LogQuizInvitationEmailSentToEmail(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Information, "Sending quiz results to {email}. Score: {score}/{total} ({percentage:F1}%)")]
    static partial void LogSendingQuizResultsToEmailScoreScoreTotalPercentageF1(ILogger<EmailService> logger,
        string email, int score, int total, double percentage);

    [LoggerMessage(LogLevel.Information, "Quiz results email sent to {email}")]
    static partial void LogQuizResultsEmailSentToEmail(ILogger<EmailService> logger, string email);

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

    private record LanguageContent(
        string QuizUrl,
        Dictionary<string, string> Translations
    );

    private static Dictionary<string, Dictionary<string, string>> GetInvitationTranslations() => new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["displayName"] = "English",
            ["questions"] = "Questions",
            ["timeLimit"] = "Time Limit",
            ["minutes"] = "minutes",
            ["validDays"] = "⏰ This reftest is valid for 7 days",
            ["greeting"] = "Hello",
            ["inviteText"] =
                "You have been invited to take the IHF Rules RefTest. Click the button below to start your reftest:",
            ["startButton"] = "Start RefTest"
        },
        ["nl"] = new Dictionary<string, string>
        {
            ["displayName"] = "Nederlands",
            ["questions"] = "Vragen",
            ["timeLimit"] = "Tijdslimiet",
            ["minutes"] = "minuten",
            ["validDays"] = "⏰ Deze reftest is 7 dagen geldig",
            ["greeting"] = "Hallo",
            ["inviteText"] =
                "Je bent uitgenodigd om deel te nemen aan de IHF Regels RefTest. Klik op de knop hieronder om je reftest te starten:",
            ["startButton"] = "Start RefTest"
        },
        ["fr"] = new Dictionary<string, string>
        {
            ["displayName"] = "Français",
            ["questions"] = "Questions",
            ["timeLimit"] = "Limite de Temps",
            ["minutes"] = "minutes",
            ["validDays"] = "⏰ Ce reftest est valide pendant 7 jours",
            ["greeting"] = "Bonjour",
            ["inviteText"] =
                "Vous êtes invité à participer au RefTest des Règles IHF. Cliquez sur le bouton ci-dessous pour commencer votre reftest:",
            ["startButton"] = "Démarrer le RefTest"
        },
        ["de"] = new Dictionary<string, string>
        {
            ["displayName"] = "Deutsch",
            ["questions"] = "Fragen",
            ["timeLimit"] = "Zeitlimit",
            ["minutes"] = "Minuten",
            ["validDays"] = "⏰ Dieses Reftest ist 7 Tage lang gültig",
            ["greeting"] = "Hallo",
            ["inviteText"] =
                "Sie wurden eingeladen, am IHF-Regeln-RefTest teilzunehmen. Klicken Sie auf die Schaltfläche unten, um Ihr Reftest zu starten:",
            ["startButton"] = "RefTest starten"
        }
    };

    private static Dictionary<string, Dictionary<string, string>> GetResultsTranslations() => new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["displayName"] = "English",
            ["passed"] = "PASSED",
            ["notPassed"] = "NOT PASSED",
            ["yourScore"] = "Your Score",
            ["percentage"] = "Percentage",
            ["passedMessage"] = "🎉 Congratulations! You passed the reftest!",
            ["failedMessage"] = "📚 Keep studying and good luck next time!",
            ["greeting"] = "Dear",
            ["passedText"] =
                "Congratulations! You have successfully passed the IHF Rules RefTest! Your knowledge of handball regulations is excellent.",
            ["failedText"] =
                "Thank you for taking the IHF Rules RefTest. A passing score is 80% or higher. Please review the rules and try again.",
            ["pdfNote"] = "📎 <strong>Detailed results are available in the attached PDF documents</strong>"
        },
        ["nl"] = new Dictionary<string, string>
        {
            ["displayName"] = "Nederlands",
            ["passed"] = "GESLAAGD",
            ["notPassed"] = "NIET GESLAAGD",
            ["yourScore"] = "Jouw Score",
            ["percentage"] = "Percentage",
            ["passedMessage"] = "🎉 Gefeliciteerd! Je bent geslaagd!",
            ["failedMessage"] = "📚 Blijf studeren en veel succes de volgende keer!",
            ["greeting"] = "Hallo",
            ["passedText"] =
                "Gefeliciteerd! Je bent geslaagd voor de IHF Regels RefTest! Je kennis van de handbalreglementen is uitstekend.",
            ["failedText"] =
                "Bedankt voor het maken van de IHF Regels RefTest. Een slaagpercentage is 80% of hoger. Bekijk de regels en probeer het opnieuw.",
            ["pdfNote"] =
                "📎 <strong>Gedetailleerde resultaten zijn beschikbaar in de bijgevoegde PDF-documenten</strong>"
        },
        ["fr"] = new Dictionary<string, string>
        {
            ["displayName"] = "Français",
            ["passed"] = "RÉUSSI",
            ["notPassed"] = "NON RÉUSSI",
            ["yourScore"] = "Votre Score",
            ["percentage"] = "Pourcentage",
            ["passedMessage"] = "🎉 Félicitations! Vous avez réussi!",
            ["failedMessage"] = "📚 Continuez à étudier et bonne chance la prochaine fois!",
            ["greeting"] = "Bonjour",
            ["passedText"] =
                "Félicitations! Vous avez réussi le RefTest des Règles IHF! Votre connaissance des règles de handball est excellente.",
            ["failedText"] =
                "Merci d'avoir participé au RefTest des Règles IHF. Un score de 80% ou plus est requis pour réussir. Veuillez réviser les règles et réessayer.",
            ["pdfNote"] = "📎 <strong>Les résultats détaillés sont disponibles dans les documents PDF joints</strong>"
        },
        ["de"] = new Dictionary<string, string>
        {
            ["displayName"] = "Deutsch",
            ["passed"] = "BESTANDEN",
            ["notPassed"] = "NICHT BESTANDEN",
            ["yourScore"] = "Ihre Punktzahl",
            ["percentage"] = "Prozentsatz",
            ["passedMessage"] = "🎉 Herzlichen Glückwunsch! Sie haben bestanden!",
            ["failedMessage"] = "📚 Lernen Sie weiter und viel Glück beim nächsten Mal!",
            ["greeting"] = "Hallo",
            ["passedText"] =
                "Herzlichen Glückwunsch! Sie haben das IHF-Regeln-RefTest bestanden! Ihre Kenntnisse der Handballregeln sind ausgezeichnet.",
            ["failedText"] =
                "Vielen Dank, dass Sie am IHF-Regeln-RefTest teilgenommen haben. Eine Punktzahl von 80% oder höher ist erforderlich zum Bestehen. Bitte überprüfen Sie die Regeln und versuchen Sie es erneut.",
            ["pdfNote"] = "📎 <strong>Detaillierte Ergebnisse sind in den beigefügten PDF-Dokumenten verfügbar</strong>"
        }
    };

    private List<LanguageContent> GetEnabledLanguagesForInvitation(string token)
    {
        var translations = GetInvitationTranslations();
        return
        [
            .. languageConfiguration.EnabledLanguages
                .Where(lang => translations.ContainsKey(lang))
                .Select(lang => new LanguageContent(
                    $"{configuration.BaseUrl}/quiz/{token}?lang={lang}",
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

    private static string BuildInvitationLanguageSection(LanguageContent langContent, string name,
        int numberOfQuestions, int maxTimeInMinutes, bool isLast)
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
                
                <!-- Quiz Details -->
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin-bottom: 20px; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📋 RefTest Details</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["questions"]}:</strong> {numberOfQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["timeLimit"]}:</strong> {maxTimeInMinutes} {t["minutes"]}</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>{t["validDays"]}</em></p>
                </div>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>{t["greeting"]} {name},</p>
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>{t["inviteText"]}</p>
                
                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{langContent.QuizUrl}' style='background-color: #e30613; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;'>{t["startButton"]}</a>
                </div>
            </div>{separator}";
    }

    private string BuildResultsLanguageSection(LanguageContent langContent, string name, int score, int totalQuestions,
        double percentage, bool passed, string resultColor, string resultBgColor, string resultIcon, bool isLast)
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
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["yourScore"]}:</strong> {score} / {totalQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["percentage"]}:</strong> {percentage:F1}%</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>{(passed ? t["passedMessage"] : t["failedMessage"])}</em></p>
                </div>

                <!-- Greeting -->
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>{t["greeting"]} {name},</p>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>
                    {(passed ? t["passedText"] : t["failedText"])}
                </p>

                <!-- PDF Reference -->
                <div style='background-color: #f5f5f5; border-left: 4px solid #e30613; padding: 16px; margin: 16px 0; border-radius: 4px;'>
                    <p style='margin: 0; color: #404040; font-size: 14px;'>{t["pdfNote"]} ({string.Join(", ", languageConfiguration.EnabledLanguages.Select(l => GetInvitationTranslations()[l]["displayName"]))}).</p>
                </div>
            </div>{separator}";
    }
}

public class EmailException(string email) : Exception($"An error occurred while sending the email to {email}");

public record EmailAttachment(string FileName, byte[] Content);