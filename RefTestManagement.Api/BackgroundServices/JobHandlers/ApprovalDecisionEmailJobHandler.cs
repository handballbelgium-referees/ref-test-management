using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Tells the creator whether their RefTests were approved or rejected.
/// </summary>
public sealed class ApprovalDecisionEmailJobHandler(
    IEmailService emailService,
    ILogger<ApprovalDecisionEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<ApprovalDecisionEmailPayload>(job, logger);

        var items = payload.RefTests
            .Select(rt => ($"{rt.FirstName} {rt.LastName}", rt.Email, rt.ScheduledAt))
            .ToList();

        await emailService.SendApprovalDecisionAsync(
            payload.CreatorName,
            payload.CreatorEmail,
            payload.ApproverName,
            payload.IsApproved,
            payload.RejectionReason,
            payload.TitleValue,
            items,
            cancellationToken);

        ServiceLoggerMessages.LogApprovalDecisionEmailSent(
            logger,
            payload.IsApproved ? "approved" : "rejected",
            LogRedaction.MaskEmail(payload.CreatorEmail),
            payload.RefTests.Count);
    }
}
