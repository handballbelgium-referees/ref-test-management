namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public sealed record RefTestReportSnapshot(
    Guid RefTestId,
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
    string? Language,
    TimeSpan? Duration);

public interface IRefTestReportDataSource
{
    Task<IReadOnlyList<RefTestReportSnapshot>> GetAsync(
        IReadOnlyCollection<Guid> refTestIds,
        CancellationToken cancellationToken);
}
