using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Clears expired, unverified export challenges and verified requests with terminal, inactive
/// delivery jobs.
/// </summary>
public sealed class PersonalDataExportRequestCleanupService(
    IServiceProvider serviceProvider,
    PrivacyChallengeConfiguration configuration,
    ILogger<PersonalDataExportRequestCleanupService> logger) : PollingBackgroundService
{
    private const int BatchSize = 500;
    private static readonly JsonSerializerOptions JobPayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override TimeSpan Interval => TimeSpan.FromMinutes(configuration.CleanupIntervalMinutes);

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var count = await ClearExpiredChallengesAsync(context, DateTime.UtcNow, cancellationToken);
        if (count > 0)
            logger.LogInformation(
                "Cleared {Count} expired or terminal personal-data export requests",
                count);
    }

    // Do not attach database exception details: expired challenge rows contain participant
    // addresses and protected key material.
    protected override void LogFailure(Exception exception) =>
        logger.LogError("Personal-data export request cleanup failed.");

    internal static async Task<int> ClearExpiredChallengesAsync(
        RefTestManagementContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var terminalDeliveryRequestIds = await GetTerminalDeliveryRequestIdsAsync(
            context,
            cancellationToken);
        var expiredRequests = await context.PersonalDataExportRequests
            .Where(PersonalDataExportRequestCleanupQueries.IsDueForCleanup(
                now,
                terminalDeliveryRequestIds))
            .OrderBy(request => request.ExpiresAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        // Recheck after selecting requests: an active duplicate may have appeared after the
        // initial terminal-job snapshot.
        var stillTerminalDeliveryRequestIds = expiredRequests.Any(request => request.VerifiedAt is not null)
            ? await GetTerminalDeliveryRequestIdsAsync(context, cancellationToken)
            : [];

        var changed = 0;
        foreach (var request in expiredRequests)
        {
            var wasCleared = request.VerifiedAt is null
                ? request.ClearExpiredChallenge(now)
                : stillTerminalDeliveryRequestIds.Contains(request.Id)
                  && request.MarkPersonalDataExportDeliveryFailed(
                    now,
                    PersonalDataExportDeliveryFailureCode.DeliveryOutcomeUnknown,
                    isTerminal: true);
            if (wasCleared)
                changed++;
        }

        if (changed > 0)
            await context.SaveChangesWithRetryAsync(cancellationToken);

        return changed;
    }

    internal static async Task<Guid[]> GetTerminalDeliveryRequestIdsAsync(
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var deliveryJobs = await context.Jobs
            .AsNoTracking()
            .Where(job => job.JobType == JobType.PersonalDataExportDeliveryEmail
                          && (job.Status == JobStatus.Failed
                              || job.Status == JobStatus.Pending
                              || job.Status == JobStatus.Processing))
            .Select(job => new { job.Status, job.Payload })
            .ToListAsync(cancellationToken);

        var terminalDeliveryRequestIds = new HashSet<Guid>();
        var activeDeliveryRequestIds = new HashSet<Guid>();
        foreach (var deliveryJob in deliveryJobs)
        {
            try
            {
                var delivery = JsonSerializer.Deserialize<PersonalDataExportDeliveryEmailPayload>(
                    deliveryJob.Payload,
                    JobPayloadOptions);
                if (delivery is not null)
                {
                    if (deliveryJob.Status == JobStatus.Failed)
                        terminalDeliveryRequestIds.Add(delivery.RequestId);
                    else
                        activeDeliveryRequestIds.Add(delivery.RequestId);
                }
            }
            catch (JsonException)
            {
                // An unreadable job cannot be associated with a request.
            }
        }

        terminalDeliveryRequestIds.ExceptWith(activeDeliveryRequestIds);
        return terminalDeliveryRequestIds.ToArray();
    }
}
