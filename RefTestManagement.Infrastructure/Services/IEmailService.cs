using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IEmailService
{
    Task SendRefTestInvitationAsync(string name, string email, string token, int numberOfQuestions,
        int maxTimeInMinutes, CancellationToken cancellationToken);

    Task SendRefTestResultsAsync(string name, string email, int questionScore, int answerScore, int totalQuestions,
        int answerTotal, double percentage, List<string> selectedAnswerIds, List<string> wrongQuestionIds,
        List<string> wrongAnswerIds, List<Question> questionsWithCorrectAnswers, bool scheduleEmail,
        CancellationToken cancellationToken);

    Task SendReportEmailAsync(string recipientEmail, byte[] excelReport, byte[] pdfReport, string timestamp,
        int refTestCount, CancellationToken cancellationToken);
}