using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Sends a participant their invitation and records that it went out.
/// </summary>
public sealed class InvitationEmailJobHandler(
    IEmailService emailService,
    RefTestManagementContext context,
    IRefTestSubscriptionService subscriptionService,
    ILogger<InvitationEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<InvitationEmailPayload>(job, logger);

        ServiceLoggerMessages.LogSendingInvitationEmail(logger, payload.RefTestId);

        await emailService.SendRefTestInvitationAsync(
            payload.RefTestId,
            payload.Name,
            payload.Email,
            payload.Token,
            payload.NumberOfQuestions,
            payload.MaxTimeInMinutes,
            cancellationToken);

        // Mark the RefTest invitation as sent
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(r => r.Id == payload.RefTestId, cancellationToken);

        if (refTest != null)
        {
            refTest.SendInvitation();
            await context.SaveChangesWithRetryAsync(cancellationToken);

            // Publish subscription event
            await subscriptionService.PublishInvitationSentAsync(
                refTest.Id,
                refTest.InvitationSentAt!.Value,
                cancellationToken);
        }
    }
}
