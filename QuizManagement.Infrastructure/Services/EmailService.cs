using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace QuizManagement.Infrastructure.Services;

public partial class EmailService(
    ILogger<EmailService> logger,
    EmailConfiguration configuration)
    : IEmailService
{
    public async Task SendQuizInvitationAsync(string email, string token, int numberOfQuestions, int maxTimeInMinutes)
    {
        var quizUrl = $"{configuration.BaseUrl}/quiz/{token}";

        const string subject = "IHF Rules Quiz Invitation";
        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; border-radius: 8px; padding: 30px; margin-bottom: 20px;'>
        <h2 style='color: #0066cc; margin-top: 0;'>IHF Rules Quiz Invitation</h2>
        <p style='font-size: 16px;'>Hello,</p>
        <p style='font-size: 16px;'>You have been invited to take the IHF Rules Quiz.</p>
        
        <div style='background-color: white; border-left: 4px solid #0066cc; padding: 15px; margin: 20px 0; border-radius: 4px;'>
            <h3 style='margin-top: 0; color: #0066cc;'>Quiz Details:</h3>
            <ul style='list-style: none; padding-left: 0;'>
                <li style='padding: 5px 0;'><strong>Number of Questions:</strong> {numberOfQuestions}</li>
                <li style='padding: 5px 0;'><strong>Maximum Time:</strong> {maxTimeInMinutes} minutes</li>
            </ul>
        </div>
        
        <p style='font-size: 16px;'>Click the button below to start your quiz:</p>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{quizUrl}' style='background-color: #0066cc; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>Start Quiz</a>
        </div>
        
        <p style='font-size: 14px; color: #666;'>Or copy and paste this link into your browser:</p>
        <p style='font-size: 14px; word-break: break-all; color: #0066cc;'>{quizUrl}</p>
        
        <p style='font-size: 14px; color: #999; margin-top: 30px;'><em>This link is valid for 7 days.</em></p>
        
        <p style='font-size: 16px; margin-top: 30px;'>Good luck!</p>
        <p style='font-size: 16px; margin-bottom: 0;'><strong>IHF Rules Quiz Team</strong></p>
    </div>
</body>
</html>";


        LogSendingQuizInvitationToEmailTokenTokenQuestionsQuestionsTimeTimeMinutes(logger, email, token, numberOfQuestions, maxTimeInMinutes, quizUrl);

        await SendEmailAsync(email, subject, emailBody);

        LogQuizInvitationEmailSentToEmail(logger, email);
    }

    public async Task SendQuizResultsAsync(string email, int score, int totalQuestions, double percentage)
    {
        const string subject = "IHF Rules Quiz - Your Results";
        var passed = percentage >= 80;
        var resultColor = passed ? "#28a745" : "#dc3545";
        var resultMessage = passed 
            ? "Congratulations! You passed the quiz!" 
            : "Unfortunately, you did not pass this time. Please review the rules and try again.";
        
        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; border-radius: 8px; padding: 30px; margin-bottom: 20px;'>
        <h2 style='color: #0066cc; margin-top: 0;'>IHF Rules Quiz - Your Results</h2>
        <p style='font-size: 16px;'>Hello,</p>
        <p style='font-size: 16px;'>Thank you for completing the IHF Rules Quiz!</p>
        
        <div style='background-color: white; border-radius: 8px; padding: 20px; margin: 20px 0; text-align: center;'>
            <h3 style='color: #0066cc; margin-top: 0;'>Your Results</h3>
            <div style='margin: 20px 0;'>
                <div style='font-size: 48px; font-weight: bold; color: {resultColor}; margin: 10px 0;'>{percentage:F1}%</div>
                <div style='font-size: 20px; color: #666; margin: 10px 0;'>{score} out of {totalQuestions}</div>
            </div>
        </div>
        
        <div style='background-color: {(passed ? "#d4edda" : "#f8d7da")}; border-left: 4px solid {resultColor}; padding: 15px; margin: 20px 0; border-radius: 4px;'>
            <p style='margin: 0; font-size: 16px; color: {resultColor}; font-weight: bold;'>{resultMessage}</p>
        </div>
        
        <p style='font-size: 16px; margin-top: 30px;'>Best regards,</p>
        <p style='font-size: 16px; margin-bottom: 0;'><strong>IHF Rules Quiz Team</strong></p>
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

        if (string.IsNullOrWhiteSpace(configuration.SendGridApiKey))
        {
            LogSendGridApiKeyNotConfiguredEmailNotSent(logger);
            return;
        }

        try
        {
            var client = new SendGridClient(configuration.SendGridApiKey);
            var from = new EmailAddress(configuration.FromEmail, configuration.FromName);
            var to = new EmailAddress(toEmail);
            
            // Create a plain text version by stripping HTML tags (simple version)
            var plainTextBody = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]*>", "");
            plainTextBody = System.Text.RegularExpressions.Regex.Replace(plainTextBody, @"\s+", " ").Trim();
            
            // Send both plain text and HTML versions
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextBody, body);
            
            var response = await client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                LogEmailSentSuccessfully(logger, toEmail);
            }
            else
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                LogEmailFailedWithStatusCode(logger, toEmail, (int)response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            LogErrorSendingEmailToEmail(logger, ex, toEmail);
            throw;
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

    [LoggerMessage(LogLevel.Warning, "SendGrid API key not configured. Email not sent.")]
    static partial void LogSendGridApiKeyNotConfiguredEmailNotSent(ILogger<EmailService> logger);

    [LoggerMessage(LogLevel.Information, "Email sent successfully to {email}")]
    static partial void LogEmailSentSuccessfully(ILogger<EmailService> logger, string email);

    [LoggerMessage(LogLevel.Warning, "Email to {email} failed with status code {statusCode}: {responseBody}")]
    static partial void LogEmailFailedWithStatusCode(ILogger<EmailService> logger, string email, int statusCode, string responseBody);

    [LoggerMessage(LogLevel.Error, "Error sending email to {email}")]
    static partial void LogErrorSendingEmailToEmail(ILogger<EmailService> logger, Exception ex, string email);
}
