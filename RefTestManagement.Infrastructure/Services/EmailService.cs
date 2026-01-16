using System.Text;
using System.Text.Json;
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
    BackgroundServiceConfiguration backgroundServiceConfiguration,
    IRefTestResultsPdfService pdfService,
    ILogoService logoService)
    : IEmailService
{
    
    public async Task SendRefTestInvitationAsync(string name, string email, string token, int numberOfQuestions,
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

        const string subject = "Referees Handball Belgium RefTest - Invitation";
        var logoBase64 = await logoService.GetLogoAsBase64Async();
        var logoTag = string.IsNullOrEmpty(logoBase64) 
            ? "" 
            : $"<img src='data:image/png;base64,{logoBase64}' alt='RefTest Logo' style='width: 100px; height: auto; margin-bottom: 10px;' />";
        
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
            {logoTag}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium RefTest - Invitation</p>
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


        var firstRefTestUrl = enabledLanguages.FirstOrDefault()?.RefTestUrl ?? $"{configuration.BaseUrl}/ref-test/{token}";
        LogSendingRefTestInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(logger, email, token,
            numberOfQuestions, maxTimeInMinutes, firstRefTestUrl);

        await SendEmailAsync(email, subject, emailBody);

        LogRefTestInvitationEmailSentToEmail(logger, email);
    }

    public async Task SendRefTestResultsAsync(string name, string email, int questionScore, int answerScore, int totalQuestions, int answerTotal, double percentage,
        List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds,
        List<Question> questionsWithCorrectAnswers, bool scheduleEmail)
    {
        const string subject = "Referees Handball Belgium RefTest - Results";
        var passed = percentage >= scoreConfiguration.PassingPercentage;
        var resultColor = passed ? "#22c55e" : "#ef4444";
        var resultBgColor = passed ? "#dcfce7" : "#fee2e2";
        var resultIcon = passed ? "✓" : "✗";

        var enabledLanguages = GetEnabledLanguagesForResults();
        var languageSections = new StringBuilder();

        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildResultsLanguageSection(langContent, name, questionScore, answerScore, totalQuestions, answerTotal, percentage,
                passed, resultColor, resultBgColor, resultIcon, isLast));
        }

        var logoBase64 = await logoService.GetLogoAsBase64Async();
        var logoTag = string.IsNullOrEmpty(logoBase64) 
            ? "" 
            : $"<img src='data:image/png;base64,{logoBase64}' alt='RefTest Logo' style='width: 100px; height: auto; margin-bottom: 10px;' />";
        
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
            {logoTag}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium RefTest - Results</p>
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
            return new EmailAttachment($"RefTest_Results_{langUpper}.pdf",
                pdfService.GenerateRefTestResultsPdf(name, lang, questionScore, answerScore, totalQuestions, answerTotal, percentage, selectedAnswerIds, wrongQuestionIds,
                    wrongAnswerIds, questionsWithCorrectAnswers));
        }).ToList();

        LogSendingRefTestResultsToEmailScoreScoreTotalPercentageF1(logger, email, questionScore, answerScore, totalQuestions, answerTotal, percentage);

        await SendEmailAsync(email, subject, emailBody, attachments, scheduleEmail);

        LogRefTestResultsEmailSentToEmail(logger, email);
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

    public async Task SendReportEmailAsync(string recipientEmail, byte[] excelReport, byte[] pdfReport, string timestamp, int refTestCount)
    {
        var subject = $"RefTest Report - {DateTime.UtcNow:dd-MM-yyyy}";
        
        // Convert to Central European Time
        var cetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var nowCet = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cetTimeZone);
        
        var enabledLanguages = GetEnabledLanguagesForReport();
        var languageSections = new StringBuilder();

        for (var i = 0; i < enabledLanguages.Count; i++)
        {
            var langContent = enabledLanguages[i];
            var isLast = i == enabledLanguages.Count - 1;
            languageSections.Append(BuildReportLanguageSection(langContent, nowCet, refTestCount, isLast));
        }
        
        var logoBase64 = await logoService.GetLogoAsBase64Async();
        var logoTag = string.IsNullOrEmpty(logoBase64) 
            ? "" 
            : $"<img src='data:image/png;base64,{logoBase64}' alt='RefTest Logo' style='width: 100px; height: auto; margin-bottom: 10px;' />";
        
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
            {logoTag}
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>RefTest Report</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium</p>
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

        var attachments = new List<EmailAttachment>
        {
            new($"RefTest_Report_{timestamp}.xlsx", excelReport),
            new($"RefTest_Report_{timestamp}.pdf", pdfReport)
        };

        await SendEmailAsync(recipientEmail, subject, emailBody, attachments);

        LogReportEmailSentToEmail(logger, recipientEmail);
    }

    [LoggerMessage(LogLevel.Information,
        "Sending RefTest invitation to {email}. Token: {token}, Questions: {questions}, Time: {time} minutes. URL: {url}")]
    static partial void LogSendingRefTestInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(
        ILogger<EmailService> logger, string email, string token, int questions, int time, string url);

    [LoggerMessage(LogLevel.Information, "RefTest invitation email sent to {email}")]
    static partial void LogRefTestInvitationEmailSentToEmail(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Information, "Sending RefTest results to {email}. QuestionScore: {questionScore}/{totalQuestions}, AnswerScore: {answerScore}/{answerTotal} ({percentage:F1}%)")]
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

    private record LanguageContent(
        string RefTestUrl,
        Dictionary<string, string> Translations
    );

    private Dictionary<string, Dictionary<string, string>> GetInvitationTranslations()
    {
        var expiration = backgroundServiceConfiguration.ExpirationIfNotStarted;

        return new Dictionary<string, Dictionary<string, string>>
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
    }

    private static Dictionary<string, Dictionary<string, string>> GetResultsTranslations() => new()
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

    private static Dictionary<string, Dictionary<string, string>> GetReportTranslations() => new()
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
                
                <!-- RefTest Details -->
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin-bottom: 20px; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📋 {t["refTestDetails"]}</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["questions"]}:</strong> {numberOfQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>{t["timeLimit"]}:</strong> {maxTimeInMinutes} {t["minutes"]}</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>{t["validDays"]}</em></p>
                </div>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>{t["greeting"]} {name},</p>
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>{t["inviteText"]}</p>
                
                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{langContent.RefTestUrl}' style='background-color: #e30613; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;'>{t["startButton"]}</a>
                </div>
            </div>{separator}";
    }

    private string BuildResultsLanguageSection(LanguageContent langContent, string name, int questionScore, int answerScore, int totalQuestions, int answerTotal,
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
                    <p style='margin: 0; color: #404040; font-size: 14px;'>{t["pdfNote"]} ({string.Join(", ", languageConfiguration.EnabledLanguages.Select(l => GetInvitationTranslations()[l]["displayName"]))}).</p>
                </div>
            </div>{separator}";
    }

    private static string BuildReportLanguageSection(LanguageContent langContent, DateTime reportDate, int refTestCount, bool isLast)
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

public class EmailException(string email) : Exception($"An error occurred while sending the email to {email}");

public record EmailAttachment(string FileName, byte[] Content);

