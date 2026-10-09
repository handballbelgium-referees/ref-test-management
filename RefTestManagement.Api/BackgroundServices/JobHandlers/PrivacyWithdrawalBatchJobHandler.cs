using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Infrastructure;
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

        var retryScheduledCount = 0;
        var exhaustedTargetCount = 0;
        foreach (var targetId in targetIds)
        {
            TargetProcessingOutcome outcome;
            try
            {
                outcome = await ProcessTargetAsync(payload.BatchId, targetId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Persist a fixed failure category and bounded retry state; never retain the
                // exception, which may contain participant data from a provider.
                outcome = await RecordTargetFailureAsync(
                    payload.BatchId,
                    targetId,
                    cancellationToken);
            }

            if (outcome == TargetProcessingOutcome.RetryScheduled)
                retryScheduledCount++;
            else if (outcome == TargetProcessingOutcome.RetryExhausted)
                exhaustedTargetCount++;
        }

        if (retryScheduledCount > 0)
            logger.LogWarning(
                "Privacy-withdrawal batch scheduled retries for {TargetCount} target(s).",
                retryScheduledCount);
        if (exhaustedTargetCount > 0)
            logger.LogWarning(
                "Privacy-withdrawal batch exhausted retries for {TargetCount} target(s).",
                exhaustedTargetCount);

        int? terminalExhaustedTargetCount;
        try
        {
            terminalExhaustedTargetCount = await MarkBatchCompletedIfReadyAsync(payload.BatchId, cancellationToken);
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

        if (terminalExhaustedTargetCount is > 0)
            throw new JobPayloadException("One or more privacy-withdrawal targets exhausted their retries.");
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
            .Where(target => target.BatchId == batchId
                             && target.CompletedAt == null
                             && target.RetryExhaustedAt == null)
            .OrderBy(target => target.RefTestId)
            .Select(target => target.RefTestId)
            .ToListAsync(cancellationToken);
    }

    private async Task<TargetProcessingOutcome> ProcessTargetAsync(
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
            return TargetProcessingOutcome.NoFailure;
        if (target.CompletedAt is not null)
            return TargetProcessingOutcome.NoFailure;
        if (target.RetryExhaustedAt is not null)
            return TargetProcessingOutcome.RetryExhausted;

        var now = DateTime.UtcNow;
        if (target.AttemptCount >= PrivacyWithdrawalBatchTarget.MaximumAttempts)
        {
            target.MarkRetryLimitReached(now);
            await context.SaveChangesWithRetryAsync(cancellationToken);
            return TargetProcessingOutcome.RetryExhausted;
        }
        if (target.NextAttemptAt is { } nextAttemptAt && nextAttemptAt > now)
            return TargetProcessingOutcome.NoFailure;

        var refTest = await context.RefTests
            .SingleOrDefaultAsync(candidate => candidate.Id == refTestId, cancellationToken);
        if (refTest is null)
        {
            target.MarkCompleted(now);
            await context.SaveChangesWithRetryAsync(cancellationToken);
            return TargetProcessingOutcome.NoFailure;
        }

        var needsAttempt = !refTest.IsAnonymized || target.ErasureStartedAt is not null;
        if (!needsAttempt)
        {
            target.MarkCompleted(now);
            await context.SaveChangesWithRetryAsync(cancellationToken);
            return TargetProcessingOutcome.NoFailure;
        }

        if (!target.TryStartAttempt(now))
            return TargetProcessingOutcome.NoFailure;
        if (!refTest.IsAnonymized)
            target.MarkErasureStarted(now);
        await context.SaveChangesWithRetryAsync(cancellationToken);

        var subscriptionService = scope.ServiceProvider.GetRequiredService<IRefTestSubscriptionService>();
        if (!refTest.IsAnonymized)
        {
            var erasureService = scope.ServiceProvider.GetRequiredService<IRefTestPrivacyErasureService>();
            await erasureService.EraseAsync(refTest, ErasureInitiator.Participant, cancellationToken);
        }

        // EraseAsync owns and commits the per-record transaction. Publish only after it returns.
        // If a previous attempt committed erasure but failed before publishing, repeat the update.
        await subscriptionService.PublishRefTestAnonymizedAsync(
            refTest.Id,
            refTest.Status,
            refTest.FullName,
            refTest.Email,
            cancellationToken);

        target.MarkCompleted(DateTime.UtcNow);
        await context.SaveChangesWithRetryAsync(cancellationToken);
        return TargetProcessingOutcome.NoFailure;
    }

    private async Task<TargetProcessingOutcome> RecordTargetFailureAsync(
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

        if (target is null || target.CompletedAt is not null)
            return TargetProcessingOutcome.NoFailure;
        if (target.RetryExhaustedAt is not null)
            return TargetProcessingOutcome.RetryExhausted;

        var failedAt = DateTime.UtcNow;
        if (target.AttemptCount >= PrivacyWithdrawalBatchTarget.MaximumAttempts)
        {
            target.MarkRetryLimitReached(failedAt);
        }
        else
        {
            if (target.NextAttemptAt is { } nextAttemptAt)
            {
                if (nextAttemptAt > failedAt || !target.TryStartAttempt(failedAt))
                    return TargetProcessingOutcome.NoFailure;
            }
            else if (target.AttemptCount == 0 && !target.TryStartAttempt(failedAt))
            {
                return TargetProcessingOutcome.NoFailure;
            }

            target.RecordProcessingFailure(failedAt);
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);
        return target.RetryExhaustedAt is null
            ? TargetProcessingOutcome.RetryScheduled
            : TargetProcessingOutcome.RetryExhausted;
    }

    private async Task<int?> MarkBatchCompletedIfReadyAsync(Guid batchId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var batch = await context.PrivacyWithdrawalBatches
            .SingleOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

        if (batch is null)
            return null;
        if (batch.CompletedAt is not null)
        {
            return await context.PrivacyWithdrawalBatchTargets
                .CountAsync(
                    target => target.BatchId == batchId && target.RetryExhaustedAt != null,
                    cancellationToken);
        }

        var hasPendingTargets = await context.PrivacyWithdrawalBatchTargets
            .AnyAsync(
                target => target.BatchId == batchId
                          && target.CompletedAt == null
                          && target.RetryExhaustedAt == null,
                cancellationToken);
        if (hasPendingTargets)
            return null;

        var exhaustedTargetCount = await context.PrivacyWithdrawalBatchTargets
            .CountAsync(
                target => target.BatchId == batchId && target.RetryExhaustedAt != null,
                cancellationToken);
        if (batch.MarkCompleted(DateTime.UtcNow, exhaustedTargetCount))
            await context.SaveChangesWithRetryAsync(cancellationToken);

        return exhaustedTargetCount;
    }

    private enum TargetProcessingOutcome
    {
        NoFailure,
        RetryScheduled,
        RetryExhausted
    }
}

/// <summary>A retryable batch failure that does not persist participant or target details.</summary>
public sealed class PrivacyWithdrawalBatchProcessingException()
    : Exception("Privacy-withdrawal batch processing did not complete.");
