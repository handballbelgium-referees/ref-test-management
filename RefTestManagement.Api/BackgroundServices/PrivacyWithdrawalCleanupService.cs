using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>Clears expired withdrawal challenges and completed durable batch targets.</summary>
public sealed class PrivacyWithdrawalCleanupService(
    IServiceProvider serviceProvider,
    PersonalDataExportConfiguration configuration,
    ILogger<PrivacyWithdrawalCleanupService> logger) : BackgroundService
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
                var now = DateTime.UtcNow;
                var expiredChallenges = await ClearExpiredChallengesAsync(context, now, stoppingToken);
                var completedTargets = await ClearCompletedBatchTargetsAsync(context, stoppingToken);

                if (expiredChallenges > 0)
                    logger.LogInformation("Cleared {Count} expired privacy-withdrawal challenges", expiredChallenges);
                if (completedTargets > 0)
                    logger.LogInformation("Cleared {Count} completed privacy-withdrawal targets", completedTargets);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Challenge rows retain participant addresses and protected keys until they expire.
                logger.LogError("Privacy-withdrawal cleanup failed.");
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
        var expiredChallenges = await context.PrivacyWithdrawalChallenges
            .Where(PrivacyWithdrawalCleanupQueries.IsDueForChallengeCleanup(now))
            .OrderBy(challenge => challenge.ExpiresAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var challenge in expiredChallenges)
        {
            if (challenge.ClearExpiredChallenge(now))
                changed++;
        }

        if (changed > 0)
            await context.SaveChangesWithRetryAsync(cancellationToken);

        return changed;
    }

    internal static async Task<int> ClearCompletedBatchTargetsAsync(
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var completedTargets = await context.PrivacyWithdrawalBatchTargets
            .Where(target => context.PrivacyWithdrawalBatches
                .Any(batch => batch.Id == target.BatchId && batch.CompletedAt != null))
            .OrderBy(target => target.CompletedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (completedTargets.Count == 0)
            return 0;

        context.PrivacyWithdrawalBatchTargets.RemoveRange(completedTargets);
        await context.SaveChangesWithRetryAsync(cancellationToken);
        return completedTargets.Count;
    }
}
