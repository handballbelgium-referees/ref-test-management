using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Processes durable withdrawal targets independently so one RefTest failure cannot block the
/// remaining records or discard progress already committed.
/// </summary>
public sealed class PrivacyWithdrawalBatchJobHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<PrivacyWithdrawalBatchJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<PrivacyWithdrawalBatchPayload>(job, logger);
        List<Guid> targetIds;
        try
        {
            targetIds = await LoadPendingTargetIdsAsync(payload.BatchId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JobPayloadException)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogError("Privacy-withdrawal batch targets could not be loaded.");
            throw new PrivacyWithdrawalBatchProcessingException();
        }

        var failedTargetCount = 0;
        foreach (var targetId in targetIds)
        {
            try
            {
                await ProcessTargetAsync(payload.BatchId, targetId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Continue so a single bad RefTest does not keep later targets from completing.
                // The fixed log message and count contain no target or participant data.
                failedTargetCount++;
            }
        }

        if (failedTargetCount > 0)
        {
            logger.LogWarning(
                "Privacy-withdrawal batch left {FailedTargetCount} target(s) for retry.",
                failedTargetCount);
            throw new PrivacyWithdrawalBatchProcessingException();
        }

        try
        {
            await MarkBatchCompletedIfReadyAsync(payload.BatchId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogError("Privacy-withdrawal batch completion could not be recorded.");
            throw new PrivacyWithdrawalBatchProcessingException();
        }
    }

    private async Task<List<Guid>> LoadPendingTargetIdsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var batch = await context.PrivacyWithdrawalBatches
            .SingleOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

        if (batch is null)
            throw new JobPayloadException("The privacy-withdrawal batch is no longer available.");
        if (batch.CompletedAt is not null)
            return [];

        return await context.PrivacyWithdrawalBatchTargets
            .Where(target => target.BatchId == batchId && target.CompletedAt == null)
            .OrderBy(target => target.RefTestId)
            .Select(target => target.RefTestId)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessTargetAsync(
        Guid batchId,
        Guid refTestId,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var target = await context.PrivacyWithdrawalBatchTargets
            .SingleOrDefaultAsync(
                candidate => candidate.BatchId == batchId && candidate.RefTestId == refTestId,
                cancellationToken);

        if (target is null)
            throw new PrivacyWithdrawalBatchProcessingException();
        if (target.CompletedAt is not null)
            return;

        var refTest = await context.RefTests
            .SingleOrDefaultAsync(candidate => candidate.Id == refTestId, cancellationToken);
        if (refTest is null)
        {
            target.MarkCompleted(DateTime.UtcNow);
            await context.SaveChangesWithRetryAsync(cancellationToken);
            return;
        }

        var subscriptionService = scope.ServiceProvider.GetRequiredService<IRefTestSubscriptionService>();
        if (refTest.IsAnonymized)
        {
            // A target already anonymized before its first attempt is complete. If a previous
            // attempt began erasure and then failed before recording completion, re-publish the
            // already-committed update before completing it.
            if (target.ErasureStartedAt is not null)
            {
                await subscriptionService.PublishRefTestAnonymizedAsync(
                    refTest.Id,
                    refTest.Status,
                    refTest.FullName,
                    refTest.Email,
                    cancellationToken);
            }

            target.MarkCompleted(DateTime.UtcNow);
            await context.SaveChangesWithRetryAsync(cancellationToken);
            return;
        }

        if (target.MarkErasureStarted(DateTime.UtcNow))
            await context.SaveChangesWithRetryAsync(cancellationToken);

        var erasureService = scope.ServiceProvider.GetRequiredService<IRefTestPrivacyErasureService>();
        await erasureService.EraseAsync(refTest, ErasureInitiator.Participant, cancellationToken);

        // EraseAsync owns and commits the per-record transaction. Publish only after it returns.
        await subscriptionService.PublishRefTestAnonymizedAsync(
            refTest.Id,
            refTest.Status,
            refTest.FullName,
            refTest.Email,
            cancellationToken);

        target.MarkCompleted(DateTime.UtcNow);
        await context.SaveChangesWithRetryAsync(cancellationToken);
    }

    private async Task MarkBatchCompletedIfReadyAsync(Guid batchId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var batch = await context.PrivacyWithdrawalBatches
            .SingleOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

        if (batch is null || batch.CompletedAt is not null)
            return;

        var hasPendingTargets = await context.PrivacyWithdrawalBatchTargets
            .AnyAsync(
                target => target.BatchId == batchId && target.CompletedAt == null,
                cancellationToken);
        if (hasPendingTargets)
            throw new PrivacyWithdrawalBatchProcessingException();

        if (batch.MarkCompleted(DateTime.UtcNow))
            await context.SaveChangesWithRetryAsync(cancellationToken);
    }
}

/// <summary>A retryable batch failure that does not persist participant or target details.</summary>
public sealed class PrivacyWithdrawalBatchProcessingException()
    : Exception("Privacy-withdrawal batch processing did not complete.");
