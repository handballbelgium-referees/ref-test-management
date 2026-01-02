using QuizManagement.Application.Models;

namespace QuizManagement.Infrastructure.Services;

public interface IEmailService
{
    Task SendQuizInvitationAsync(string name, string email, string token, int numberOfQuestions, int maxTimeInMinutes);
    Task SendQuizResultsAsync(string name, string email, int questionScore, int answerScore, int totalQuestions, int answerTotal, double percentage, List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds, List<Question> questionsWithCorrectAnswers);
    Task SendReportEmailAsync(string recipientEmail, byte[] excelReport, byte[] pdfReport, string timestamp, int sessionCount);
}

