using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Hosting.BackgroundServices.JobHandlers;

/// <summary>
/// Notifies users with the approve permission in the configured identity provider (Auth0 API audience
/// or Keycloak client roles) when RefTests are waiting on them.
/// </summary>
public sealed class ApprovalNotificationEmailJobHandler(
    IIdentityUserDirectory userDirectory,
    IEmailService emailService,
    EmailConfiguration emailConfig,
    ILogger<ApprovalNotificationEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<ApprovalNotificationEmailPayload>(job, logger);

        var approvers = await userDirectory.GetUsersWithPermissionAsync(
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

        // One approver's failed send must not stop the rest. The job retries after all sends are
        // attempted; the provider's idempotency key keeps already-delivered messages from repeating.
        var failures = new List<Exception>();
        foreach (var approver in approvers)
        {
            try
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(ex);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Approval notification failed for {failures.Count} of {approvers.Count} approvers; the job will retry.",
                new AggregateException(failures));
        }

        ServiceLoggerMessages.LogApprovalNotificationSent(logger, approvers.Count, payload.RefTests.Count);
    }
}
