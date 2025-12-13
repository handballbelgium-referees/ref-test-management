using QuizManagement.Application.Models;

namespace QuizManagement.Infrastructure.Services;

public interface IEmailService
{
    Task SendQuizInvitationAsync(string name, string email, string token, int numberOfQuestions, int maxTimeInMinutes);
    Task SendQuizResultsAsync(string email, int score, int totalQuestions, double percentage, List<string> selectedAnswerIds, List<Question> questionsWithCorrectAnswers);
}

