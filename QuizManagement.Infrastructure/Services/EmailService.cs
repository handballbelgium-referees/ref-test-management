using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace QuizManagement.Infrastructure.Services;

public partial class EmailService(
    ILogger<EmailService> logger,
    EmailConfiguration configuration)
    : IEmailService
{
    public async Task SendQuizInvitationAsync(string email, string token, int numberOfQuestions, int maxTimeInMinutes)
    {
        var quizUrlEn = $"{configuration.BaseUrl}/quiz/{token}?lang=en";
        var quizUrlNl = $"{configuration.BaseUrl}/quiz/{token}?lang=nl";
        var quizUrlFr = $"{configuration.BaseUrl}/quiz/{token}?lang=fr";
        var quizUrlDe = $"{configuration.BaseUrl}/quiz/{token}?lang=de";

        const string subject = "Referees Handball Belgium Quiz Invitation";
        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif; }}
    </style>
</head>
<body style='margin: 0; padding: 0;'>
    <div style='max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden;'>
        <!-- Header with Belgian Handball Colors -->
        <div style='background-color: #b30510; padding: 40px 20px; text-align: center; border-radius: 12px 12px 0 0;'>
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>IHF Rules Quiz</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Referees Handball Belgium Quiz Invitation</p>
        </div>

        <!-- Main Content -->
        <div style='padding: 20px;'>
            <!-- English Section -->
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>English</h2>
                
                <!-- Quiz Details -->
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin-bottom: 20px; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📋 Quiz Details</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Questions:</strong> {numberOfQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Time Limit:</strong> {maxTimeInMinutes} minutes</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>⏰ This quiz is valid for 7 days</em></p>
                </div>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>Hello,</p>
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>You have been invited to take the IHF Rules Quiz. Click the button below to start your quiz:</p>
                
                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{quizUrlEn}' style='background-color: #e30613; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;'>Start Quiz</a>
                </div>
            </div>

            <!-- Separator -->
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />

            <!-- Dutch Section -->
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>Nederlands</h2>
                
                <!-- Quiz Details -->
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin-bottom: 20px; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📋 Quiz Details</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Vragen:</strong> {numberOfQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Tijdslimiet:</strong> {maxTimeInMinutes} minuten</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>⏰ Deze quiz is 7 dagen geldig</em></p>
                </div>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>Hallo,</p>
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>Je bent uitgenodigd om deel te nemen aan de IHF Regels Quiz. Klik op de knop hieronder om je quiz te starten:</p>
                
                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{quizUrlNl}' style='background-color: #e30613; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;'>Start Quiz</a>
                </div>
            </div>

            <!-- Separator -->
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />

            <!-- French Section -->
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>Français</h2>
                
                <!-- Quiz Details -->
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin-bottom: 20px; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📋 Détails du Quiz</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Questions:</strong> {numberOfQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Limite de Temps:</strong> {maxTimeInMinutes} minutes</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>⏰ Ce quiz est valide pendant 7 jours</em></p>
                </div>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>Bonjour,</p>
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>Vous êtes invité à participer au Quiz des Règles IHF. Cliquez sur le bouton ci-dessous pour commencer votre quiz:</p>
                
                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{quizUrlFr}' style='background-color: #e30613; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;'>Démarrer le Quiz</a>
                </div>
            </div>

            <!-- Separator -->
            <hr style='border: none; border-top: 1px solid #e5e5e5; margin: 30px 0;' />

            <!-- German Section -->
            <div style='padding: 0; margin-bottom: 20px;'>
                <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>Deutsch</h2>
                
                <!-- Quiz Details -->
                <div style='background-color: #fef2f2; border-left: 4px solid #e30613; padding: 20px; margin-bottom: 20px; border-radius: 4px;'>
                    <h3 style='color: #e30613; margin: 0 0 12px 0; font-size: 16px; font-weight: bold;'>📋 Quiz-Details</h3>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Fragen:</strong> {numberOfQuestions}</p>
                    <p style='margin: 8px 0; color: #404040; font-size: 15px;'><strong>Zeitlimit:</strong> {maxTimeInMinutes} Minuten</p>
                    <p style='margin: 8px 0; color: #737373; font-size: 14px;'><em>⏰ Dieses Quiz ist 7 Tage lang gültig</em></p>
                </div>

                <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>Hallo,</p>
                <p style='color: #404040; font-size: 16px; margin: 0 0 20px 0;'>Sie wurden eingeladen, am IHF-Regeln-Quiz teilzunehmen. Klicken Sie auf die Schaltfläche unten, um Ihr Quiz zu starten:</p>
                
                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{quizUrlDe}' style='background-color: #e30613; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;'>Quiz starten</a>
                </div>
            </div>

            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>Referees Handball Belgium Team</p>
            </div>
        </div>
    </div>
</body>
</html>";


        LogSendingQuizInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(logger, email, token, numberOfQuestions, maxTimeInMinutes, quizUrlEn);

        await SendEmailAsync(email, subject, emailBody);

        LogQuizInvitationEmailSentToEmail(logger, email);
    }

    public async Task SendQuizResultsAsync(string email, int score, int totalQuestions, double percentage)
    {
        const string subject = "IHF Rules Quiz - Your Results";
        var passed = percentage >= 80;
        var resultColor = passed ? "#22c55e" : "#ef4444";
        var resultBgColor = passed ? "#dcfce7" : "#fee2e2";
        var resultIcon = passed ? "✓" : "✗";
        
        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif; }}
    </style>
</head>
<body style='margin: 0; padding: 0; background-color: #f5f5f5;'>
    <!-- Header with Belgian Handball Colors -->
    <div style='background: linear-gradient(135deg, #e30613 0%, #b30510 100%); padding: 40px 20px; text-align: center;'>
        <div style='max-width: 600px; margin: 0 auto;'>
            <div style='background-color: rgba(255, 255, 255, 0.1); backdrop-filter: blur(10px); width: 64px; height: 64px; border-radius: 50%; display: inline-flex; align-items: center; justify-content: center; margin-bottom: 16px;'>
                <svg width='32' height='32' viewBox='0 0 24 24' fill='none' stroke='#ffffff' stroke-width='2'>
                    <path d='M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z'/>
                </svg>
            </div>
            <h1 style='color: #ffffff; font-size: 32px; font-weight: bold; margin: 0 0 8px 0;'>IHF Rules Quiz</h1>
            <p style='color: #fecaca; font-size: 18px; margin: 0;'>Your Results</p>
        </div>
    </div>

    <!-- Main Content -->
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <!-- English Section -->
        <div style='background-color: #ffffff; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); padding: 30px; margin-bottom: 20px;'>
            <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>English</h2>
            
            <!-- Score Display -->
            <div style='background-color: #ffffff; border-radius: 8px; padding: 25px; margin-bottom: 20px; text-align: center; border: 2px solid {resultColor};'>
                <h3 style='color: #000000; margin: 0 0 16px 0; font-size: 18px; font-weight: bold;'>Your Score</h3>
                <div style='font-size: 56px; font-weight: bold; color: {resultColor}; margin: 15px 0; line-height: 1;'>{percentage:F1}%</div>
                <div style='font-size: 20px; color: #737373; margin: 10px 0;'>{score} out of {totalQuestions} correct</div>
            </div>

            <!-- Result Status -->
            <div style='background-color: {resultBgColor}; border-left: 4px solid {resultColor}; padding: 16px; margin-bottom: 20px; border-radius: 4px; text-align: center;'>
                <p style='margin: 0; font-size: 18px; color: {resultColor}; font-weight: bold;'>{resultIcon} {(passed ? "PASSED" : "NOT PASSED")}</p>
            </div>

            <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>
                {(passed 
                    ? "Congratulations! You have successfully passed the IHF Rules Quiz! Your knowledge of handball regulations is excellent." 
                    : "Thank you for taking the IHF Rules Quiz. A passing score is 80% or higher. Please review the rules and try again.")}
            </p>
        </div>

        <!-- Dutch Section -->
        <div style='background-color: #ffffff; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); padding: 30px; margin-bottom: 20px;'>
            <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>Nederlands</h2>
            
            <!-- Score Display -->
            <div style='background-color: #ffffff; border-radius: 8px; padding: 25px; margin-bottom: 20px; text-align: center; border: 2px solid {resultColor};'>
                <h3 style='color: #000000; margin: 0 0 16px 0; font-size: 18px; font-weight: bold;'>Jouw Score</h3>
                <div style='font-size: 56px; font-weight: bold; color: {resultColor}; margin: 15px 0; line-height: 1;'>{percentage:F1}%</div>
                <div style='font-size: 20px; color: #737373; margin: 10px 0;'>{score} van de {totalQuestions} correct</div>
            </div>

            <!-- Result Status -->
            <div style='background-color: {resultBgColor}; border-left: 4px solid {resultColor}; padding: 16px; margin-bottom: 20px; border-radius: 4px; text-align: center;'>
                <p style='margin: 0; font-size: 18px; color: {resultColor}; font-weight: bold;'>{resultIcon} {(passed ? "GESLAAGD" : "NIET GESLAAGD")}</p>
            </div>

            <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>
                {(passed 
                    ? "Gefeliciteerd! Je bent geslaagd voor de IHF Regels Quiz! Je kennis van de handbalreglementen is uitstekend." 
                    : "Bedankt voor het maken van de IHF Regels Quiz. Een slaagpercentage is 80% of hoger. Bekijk de regels en probeer het opnieuw.")}
            </p>
        </div>

        <!-- French Section -->
        <div style='background-color: #ffffff; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); padding: 30px; margin-bottom: 20px; overflow: hidden;'>
            <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>Français</h2>
            
            <!-- Score Display -->
            <div style='background-color: #ffffff; border-radius: 8px; padding: 25px; margin-bottom: 20px; text-align: center; border: 2px solid {resultColor};'>
                <h3 style='color: #000000; margin: 0 0 16px 0; font-size: 18px; font-weight: bold;'>Votre Score</h3>
                <div style='font-size: 56px; font-weight: bold; color: {resultColor}; margin: 15px 0; line-height: 1;'>{percentage:F1}%</div>
                <div style='font-size: 20px; color: #737373; margin: 10px 0;'>{score} sur {totalQuestions} correct</div>
            </div>

            <!-- Result Status -->
            <div style='background-color: {resultBgColor}; border-left: 4px solid {resultColor}; padding: 16px; margin-bottom: 20px; border-radius: 4px; text-align: center;'>
                <p style='margin: 0; font-size: 18px; color: {resultColor}; font-weight: bold;'>{resultIcon} {(passed ? "RÉUSSI" : "NON RÉUSSI")}</p>
            </div>

            <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>
                {(passed 
                    ? "Félicitations! Vous avez réussi le Quiz des Règles IHF! Votre connaissance des règles de handball est excellente." 
                    : "Merci d'avoir participé au Quiz des Règles IHF. Un score de 80% ou plus est requis pour réussir. Veuillez réviser les règles et réessayer.")}
            </p>
        </div>

        <!-- German Section -->
        <div style='background-color: #ffffff; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); padding: 30px; margin-bottom: 20px; overflow: hidden;'>
            <h2 style='color: #e30613; font-size: 24px; margin: 0 0 20px 0; text-align: center;'>Deutsch</h2>
            
            <!-- Score Display -->
            <div style='background-color: #ffffff; border-radius: 8px; padding: 25px; margin-bottom: 20px; text-align: center; border: 2px solid {resultColor};'>
                <h3 style='color: #000000; margin: 0 0 16px 0; font-size: 18px; font-weight: bold;'>Ihre Punktzahl</h3>
                <div style='font-size: 56px; font-weight: bold; color: {resultColor}; margin: 15px 0; line-height: 1;'>{percentage:F1}%</div>
                <div style='font-size: 20px; color: #737373; margin: 10px 0;'>{score} von {totalQuestions} richtig</div>
            </div>

            <!-- Result Status -->
            <div style='background-color: {resultBgColor}; border-left: 4px solid {resultColor}; padding: 16px; margin-bottom: 20px; border-radius: 4px; text-align: center;'>
                <p style='margin: 0; font-size: 18px; color: {resultColor}; font-weight: bold;'>{resultIcon} {(passed ? "BESTANDEN" : "NICHT BESTANDEN")}</p>
            </div>

            <p style='color: #404040; font-size: 16px; margin: 0 0 16px 0;'>
                {(passed 
                    ? "Herzlichen Glückwunsch! Sie haben das IHF-Regeln-Quiz bestanden! Ihre Kenntnisse der Handballregeln sind ausgezeichnet." 
                    : "Vielen Dank, dass Sie am IHF-Regeln-Quiz teilgenommen haben. Eine Punktzahl von 80% oder höher ist erforderlich zum Bestehen. Bitte überprüfen Sie die Regeln und versuchen Sie es erneut.")}
            </p>
        </div>

            <!-- Footer -->
            <div style='text-align: center; color: #737373; font-size: 14px; padding: 20px 0;'>
                <p style='margin: 0; font-weight: bold; color: #000000;'>IHF Rules Quiz Team</p>
            </div>
        </div>
    </div>
</body>
</html>";


        LogSendingQuizResultsToEmailScoreScoreTotalPercentageF1(logger, email, score, totalQuestions, percentage);

        await SendEmailAsync(email, subject, emailBody);

        LogQuizResultsEmailSentToEmail(logger, email);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
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
            
            // Prepare JSON payload for Brevo API
            var emailData = new
            {
                sender = new { name = configuration.FromName, email = configuration.FromEmail },
                to = new[] { new { email = toEmail } },
                subject = subject,
                htmlContent = body,
                textContent = plainTextBody
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

    [LoggerMessage(LogLevel.Information, "Sending quiz invitation to {email}. Token: {token}, Questions: {questions}, Time: {time} minutes. URL: {url}")]
    static partial void LogSendingQuizInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(ILogger<EmailService> logger, string email, string token, int questions, int time, string url);

    [LoggerMessage(LogLevel.Information, "Quiz invitation email sent to {email}")]
    static partial void LogQuizInvitationEmailSentToEmail(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Information, "Sending quiz results to {email}. Score: {score}/{total} ({percentage:F1}%)")]
    static partial void LogSendingQuizResultsToEmailScoreScoreTotalPercentageF1(ILogger<EmailService> logger, string email, int score, int total, double percentage);

    [LoggerMessage(LogLevel.Information, "Quiz results email sent to {email}")]
    static partial void LogQuizResultsEmailSentToEmail(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Information, "Sending email to {email} with {subject} and {body}")]
    static partial void LogSendingEmailToEmailWithSubjectAndBody(ILogger<EmailService> logger, string email, string subject, string body);

    [LoggerMessage(LogLevel.Warning, "Brevo API key not configured. Email not sent.")]
    static partial void LogBrevoApiKeyNotConfiguredEmailNotSent(ILogger<EmailService> logger);

    [LoggerMessage(LogLevel.Information, "Email sent successfully to {email}")]
    static partial void LogEmailSentSuccessfully(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Warning, "Email to {email} failed with status code {statusCode}: {responseBody}")]
    static partial void LogEmailFailedWithStatusCode(ILogger<EmailService> logger, string email, int statusCode, string responseBody);

    [LoggerMessage(LogLevel.Error, "Error sending email to {email}")]
    static partial void LogErrorSendingEmailToEmail(ILogger<EmailService> logger, Exception ex, string email);
}

public class EmailException(string email) : Exception($"An error occurred while sending the email to {email}");