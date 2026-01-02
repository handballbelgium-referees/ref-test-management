namespace QuizManagement.Infrastructure.Services;

public interface IQuizReportService
{
    Task SendReportAsync(List<QuizSessionReportData> sessions, string[] recipientEmails, CancellationToken cancellationToken = default);
}

public record QuizSessionReportData(
    string TitleName,
    string FirstName,
    string LastName,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? QuestionScore,
    int QuestionTotal,
    int? AnswerScore,
    int? AnswerTotal,
    double? Percentage,
    bool Passed);

