using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Sends the staff-facing summary report for a batch of RefTests.
/// </summary>
public sealed class ReportEmailJobHandler(
    IRefTestReportService reportService,
    ILogger<ReportEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<ReportEmailPayload>(job, logger);

        ServiceLoggerMessages.LogSendingReportEmail(logger, payload.RecipientEmails.Length);

        // Convert payload data to service data
        var refTests = payload.RefTests.Select(r => new RefTestReportData(
            r.TitleName,
            r.FirstName,
            r.LastName,
            r.StartedAt,
            r.CompletedAt,
            r.QuestionScore,
            r.QuestionTotal,
            r.AnswerScore,
            r.AnswerTotal,
            r.Percentage,
            r.Passed,
            r.Language,
            r.Duration)).ToList();

        await reportService.SendReportAsync(refTests, payload.RecipientEmails, cancellationToken);
    }
}
