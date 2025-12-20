namespace QuizManagement.Infrastructure.Services;

public class EmailConfiguration
{
    public string BaseUrl { get; init; } = "http://localhost:5000";
    public string BrevoApiKey { get; init; } = string.Empty;
    public string BrevoApiUrl { get; init; } = "https://api.brevo.com/v3";
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public int ScheduledDelayMinutes { get; init; } = 0;
}

