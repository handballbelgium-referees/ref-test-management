using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Tells everyone holding the approve permission that RefTests are waiting on them.
/// </summary>
public sealed class ApprovalNotificationEmailJobHandler(
    IAuth0ManagementService auth0Service,
    IEmailService emailService,
    EmailConfiguration emailConfig,
    ILogger<ApprovalNotificationEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<ApprovalNotificationEmailPayload>(job, logger);

        var approvers = await auth0Service.GetUsersWithPermissionAsync(
            Permissions.RefTests.Approve, cancellationToken);

        if (approvers.Count == 0)
        {
            logger.LogWarning(
                "No approvers found for permission '{Permission}' — approval notification email not sent",
                Permissions.RefTests.Approve);
            return;
        }

        var items = payload.RefTests
            .Select(rt => ($"{rt.FirstName} {rt.LastName}", rt.Email, rt.ScheduledAt))
            .ToList();

        foreach (var approver in approvers)
        {
            await emailService.SendApprovalNotificationAsync(
                approver.Name,
                approver.Email,
                payload.CreatorName,
                payload.TitleValue,
                items,
                emailConfig.BaseUrl,
                cancellationToken);
        }

        ServiceLoggerMessages.LogApprovalNotificationSent(logger, approvers.Count, payload.RefTests.Count);
    }
}
