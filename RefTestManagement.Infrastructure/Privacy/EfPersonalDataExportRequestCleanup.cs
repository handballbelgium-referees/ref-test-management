using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Privacy;

public sealed class EfPersonalDataExportRequestCleanup(RefTestManagementContext context)
    : IPersonalDataExportRequestCleanup
{
    private const int BatchSize = 500;

    private static readonly JsonSerializerOptions JobPayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> ClearExpiredChallengesAsync(DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            var terminalDeliveryRequestIds = await GetTerminalDeliveryRequestIdsAsync(cancellationToken);
            var expiredRequests = await context.PersonalDataExportRequests
                .Where(PersonalDataExportRequestCleanupQueries.IsDueForCleanup(
                    now,
                    terminalDeliveryRequestIds))
                .OrderBy(request => request.ExpiresAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            var stillTerminalDeliveryRequestIds = expiredRequests.Any(request => request.VerifiedAt is not null)
                ? await GetTerminalDeliveryRequestIdsAsync(cancellationToken)
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
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<Guid[]> GetTerminalDeliveryRequestIdsAsync(CancellationToken cancellationToken)
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
                if (delivery is null)
                    continue;

                if (deliveryJob.Status == JobStatus.Failed)
                    terminalDeliveryRequestIds.Add(delivery.RequestId);
                else
                    activeDeliveryRequestIds.Add(delivery.RequestId);
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
