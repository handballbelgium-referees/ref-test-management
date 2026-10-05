using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>Reconciles incomplete withdrawal batches and clears expired or completed data.</summary>
public sealed class PrivacyWithdrawalCleanupService(
    IServiceProvider serviceProvider,
    PrivacyChallengeConfiguration configuration,
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
                var requestService = scope.ServiceProvider.GetRequiredService<IPrivacyWithdrawalRequestService>();
                var now = DateTime.UtcNow;
                await RunCleanupCycleAsync(context, requestService, now, logger, stoppingToken);
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

    internal static async Task RunCleanupCycleAsync(
        RefTestManagementContext context,
        IPrivacyWithdrawalRequestService requestService,
        DateTime now,
        ILogger<PrivacyWithdrawalCleanupService> logger,
        CancellationToken cancellationToken)
    {
        var recoveredBatches = 0;
        try
        {
            recoveredBatches = await requestService.ReconcileIncompleteBatchesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Reconciliation and the retention purges below share this scoped context. A failure
            // after the rollback leaves its staged replacement job and batch/target edits tracked as
            // Added/Modified, so the next SaveChanges would flush them outside the transaction that
            // validated them (or keep failing on them) and block the purges. Discard them first.
            context.ChangeTracker.Clear();
            logger.LogError("Privacy-withdrawal reconciliation failed.");
        }

        var expiredChallenges = await ClearExpiredChallengesAsync(context, now, cancellationToken);
        var completedTargets = await ClearCompletedBatchTargetsAsync(context, cancellationToken);

        if (recoveredBatches > 0)
            logger.LogInformation(
                "Requeued {Count} incomplete privacy-withdrawal batches",
                recoveredBatches);
        if (expiredChallenges > 0)
            logger.LogInformation("Cleared {Count} expired privacy-withdrawal challenges", expiredChallenges);
        if (completedTargets > 0)
            logger.LogInformation("Cleared {Count} completed privacy-withdrawal targets", completedTargets);
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
