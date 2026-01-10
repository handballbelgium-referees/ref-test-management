namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IRefTestReportService
{
    Task SendReportAsync(List<RefTestReportData> refTests, string[] recipientEmails, CancellationToken cancellationToken = default);
}

public record RefTestReportData(
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
    bool Passed,
    string? Language,
    TimeSpan? Duration);

