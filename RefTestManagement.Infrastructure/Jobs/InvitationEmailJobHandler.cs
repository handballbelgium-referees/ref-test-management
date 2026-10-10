using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Jobs;

/// <summary>
/// Sends a participant their invitation and records that it went out.
/// </summary>
public sealed class InvitationEmailJobHandler(
    IEmailService emailService,
    RefTestManagementContext context,
    IRefTestSubscriptionService subscriptionService,
    IRefTestInvitationTokenProtection tokenProtection,
    ILogger<InvitationEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<InvitationEmailPayload>(job, logger);

        var refTest = await context.RefTests
            .FirstOrDefaultAsync(r => r.Id == payload.RefTestId, cancellationToken);

        if (refTest is null
            || string.IsNullOrEmpty(payload.TokenHash)
            || refTest.Token != payload.TokenHash
            || string.IsNullOrEmpty(refTest.ProtectedInvitationToken))
        {
            ServiceLoggerMessages.LogSkippingStaleInvitationEmail(logger, payload.RefTestId);
            return;
        }

        var token = tokenProtection.Unprotect(refTest.ProtectedInvitationToken);
        if (refTest.Token != RefTest.HashToken(token))
            throw new InvalidOperationException("Protected invitation token does not match the stored token hash.");

        ServiceLoggerMessages.LogSendingInvitationEmail(logger, payload.RefTestId);

        var wasAccepted = await emailService.SendRefTestInvitationAsync(
            payload.RefTestId,
            payload.Name,
            payload.Email,
            token,
            payload.NumberOfQuestions,
            payload.MaxTimeInMinutes,
            cancellationToken);
        if (!wasAccepted)
            throw new EmailException(payload.Email);

        refTest.SendInvitation();
        await context.SaveChangesWithRetryAsync(cancellationToken);

        await subscriptionService.PublishInvitationSentAsync(
            refTest.Id,
            refTest.InvitationSentAt!.Value,
            cancellationToken);
    }
}
