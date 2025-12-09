namespace QuizManagement.Infrastructure.Services;

public class EmailConfiguration
{
    public string BaseUrl { get; init; } = "http://localhost:5000";
    public string SendGridApiKey { get; init; } = string.Empty;
    public string FromEmail { get; init; } = "noreply@ihf-rules.com";
    public string FromName { get; init; } = "IHF Rules Quiz";
}

