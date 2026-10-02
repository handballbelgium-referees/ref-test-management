using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>Clears recipient and challenge state for expired, unverified export requests.</summary>
public sealed class PersonalDataExportRequestCleanupService(
    IServiceProvider serviceProvider,
    PrivacyChallengeConfiguration configuration,
    ILogger<PersonalDataExportRequestCleanupService> logger) : BackgroundService
{
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
                var count = await ClearExpiredChallengesAsync(context, DateTime.UtcNow, stoppingToken);
                if (count > 0)
                    logger.LogInformation("Cleared {Count} expired personal-data export challenges", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Do not attach database exception details: expired challenge rows contain
                // participant addresses and protected key material.
                logger.LogError("Personal-data export request cleanup failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(configuration.CleanupIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal static async Task<int> ClearExpiredChallengesAsync(
        RefTestManagementContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var expiredRequests = await context.PersonalDataExportRequests
            .Where(PersonalDataExportRequestCleanupQueries.IsDueForCleanup(now))
            .OrderBy(request => request.ExpiresAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var request in expiredRequests)
        {
            if (request.ClearExpiredChallenge(now))
                changed++;
        }

        if (changed > 0)
            await context.SaveChangesWithRetryAsync(cancellationToken);

        return changed;
    }
}
