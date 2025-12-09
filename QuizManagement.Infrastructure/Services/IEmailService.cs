namespace QuizManagement.Infrastructure.Services;

public interface IEmailService
{
    Task SendQuizInvitationAsync(string email, string token, int numberOfQuestions, int maxTimeInMinutes);
    Task SendQuizResultsAsync(string email, int score, int totalQuestions, double percentage);
}

