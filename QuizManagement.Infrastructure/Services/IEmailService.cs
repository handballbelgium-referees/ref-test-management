namespace QuizManagement.Infrastructure.Services;

public interface IEmailService
{
    Task SendQuizInvitationAsync(string name, string email, string token, int numberOfQuestions, int maxTimeInMinutes);
    Task SendQuizResultsAsync(string email, int score, int totalQuestions, double percentage);
}

