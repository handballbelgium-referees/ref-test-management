using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

public sealed class PrivacyRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<PrivacyRetentionService> logger,
    PrivacyConfiguration privacyConfiguration) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);
    private const int AnonymizedRejectionRepairBatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EraseExpiredDataAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                // This loop reads and rewrites participant names and addresses, so an exception
                // raised inside it can quote them back.
                logger.LogError(LogRedaction.MaskEmails(exception), "Privacy retention cleanup failed");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task EraseExpiredDataAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var erasureService = scope.ServiceProvider.GetRequiredService<IRefTestPrivacyErasureService>();
        var cutoff = DateTime.UtcNow.AddYears(-privacyConfiguration.RetentionYears);

        var candidateIds = await context.RefTests
            .Where(PrivacyRetentionQueries.IsDueForErasure(cutoff))
            .OrderBy(refTest => refTest.Id)
            .Select(refTest => refTest.Id)
            .ToListAsync(cancellationToken);

        var erasedCount = await ProcessCandidatesAsync(
            context,
            candidateIds,
            (refTestId, ct) => erasureService.EraseIfDueForRetentionAsync(refTestId, cutoff, ct),
            logger,
            cancellationToken);

        if (erasedCount > 0)
            logger.LogInformation("Erased {Count} RefTest records that exceeded privacy retention", erasedCount);

        var repaired = await RepairAnonymizedRejectionReasonsAsync(
            context,
            erasureService,
            cancellationToken,
            logger);
        if (repaired > 0)
            logger.LogInformation("Repaired residual rejection data on {Count} already-anonymized RefTests", repaired);
    }

    internal static async Task<int> ProcessCandidatesAsync(
        RefTestManagementContext context,
        IReadOnlyCollection<Guid> candidateIds,
        Func<Guid, CancellationToken, Task<bool>> processCandidate,
        ILogger<PrivacyRetentionService>? logger,
        CancellationToken cancellationToken)
    {
        var completedCount = 0;
        foreach (var candidateId in candidateIds)
        {
            try
            {
                if (await processCandidate(candidateId, cancellationToken))
                    completedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Discard failed tracked writes before the next candidate and never log exception
                // contents: database errors can include participant values.
                context.ChangeTracker.Clear();
                logger?.LogError("Privacy retention processing failed for one RefTest candidate");
            }
        }

        return completedCount;
    }

    internal static async Task<int> RepairAnonymizedRejectionReasonsAsync(
        RefTestManagementContext context,
        IRefTestPrivacyErasureService erasureService,
        CancellationToken cancellationToken,
        ILogger<PrivacyRetentionService>? logger = null)
    {
        // EraseAsync clears the residual reason, so the next daily run advances to the next page
        // without a durable cursor or selecting already-repaired rows again.
        var refTestIds = await context.RefTests
            .Where(refTest => refTest.IsAnonymized && refTest.RejectionReason != null)
            .OrderBy(refTest => refTest.Id)
            .Take(AnonymizedRejectionRepairBatchSize)
            .Select(refTest => refTest.Id)
            .ToListAsync(cancellationToken);

        return await ProcessCandidatesAsync(
            context,
            refTestIds,
            async (refTestId, ct) =>
            {
                var refTest = await context.RefTests
                    .SingleOrDefaultAsync(candidate => candidate.Id == refTestId, ct);
                if (refTest is null)
                    return false;

                await erasureService.EraseAsync(refTest, ErasureInitiator.Operator, ct);
                return true;
            },
            logger,
            cancellationToken);
    }
}