using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

public enum SendRefTestReportFailure
{
    None,
    NoRefTests,
    NoRecipients,
    EnqueueFailed
}

public sealed record SendRefTestReportOutcome(
    int RefTestCount,
    int RecipientCount,
    SendRefTestReportFailure Failure,
    Exception? Exception = null);

public sealed class SendRefTestReportHandler(
    IRefTestReportDataSource reportDataSource,
    IReportEmailJobQueue reportEmailJobQueue,
    ReportConfiguration reportConfiguration,
    ScoreConfiguration scoreConfiguration,
    TimeProvider timeProvider)
{
    public async Task<SendRefTestReportOutcome> HandleAsync(
        IReadOnlyCollection<Guid> refTestIds,
        CancellationToken cancellationToken)
    {
        var refTests = await reportDataSource.GetAsync(refTestIds, cancellationToken);
        if (refTests.Count == 0)
            return new(0, 0, SendRefTestReportFailure.NoRefTests);

        var recipients = reportConfiguration.RecipientEmails;
        if (recipients.Length == 0)
            return new(refTests.Count, 0, SendRefTestReportFailure.NoRecipients);

        var reportData = refTests.Select(refTest => new RefTestReportPayloadData(
            refTest.RefTestId,
            refTest.TitleName,
            refTest.FirstName,
            refTest.LastName,
            refTest.StartedAt,
            refTest.CompletedAt,
            refTest.QuestionScore,
            refTest.QuestionTotal,
            refTest.AnswerScore,
            refTest.AnswerTotal,
            refTest.Percentage,
            refTest.Percentage >= scoreConfiguration.PassingPercentage,
            refTest.Language,
            refTest.Duration)).ToList();

        var payload = new ReportEmailPayload(
            recipients,
            reportData,
            timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"));

        try
        {
            await reportEmailJobQueue.EnqueueAsync(payload, cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new(
                refTests.Count,
                recipients.Length,
                SendRefTestReportFailure.EnqueueFailed,
                exception);
        }

        return new(refTests.Count, recipients.Length, SendRefTestReportFailure.None);
    }
}
