using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Security;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Privacy;

/// <summary>
/// Creates mailbox-verification challenges and atomically turns valid confirmations or
/// participant-credential requests into durable, batch-ID-only withdrawal jobs, and reconciles
/// incomplete batches on a schedule.
/// </summary>
public sealed class PrivacyWithdrawalRequestService(
    RefTestManagementContext context,
    IJobEnqueueService jobEnqueueService,
    IPersonalDataExportKeyProtection keyProtection,
    IRefTestSessionTokenService sessionTokenService,
    PrivacyChallengeConfiguration configuration,
    BackgroundJobConfiguration backgroundJobConfiguration,
    ILogger<PrivacyWithdrawalRequestService> logger,
    TimeProvider? timeProvider = null) : IPrivacyWithdrawalRequestService
{
    private const int ReconciliationBatchSize = 500;
    private const int EmailLookupBackfillBatchSize = 250;
    private const int MaximumEmailLookupConcurrencyRetries = 3;

    private static readonly JsonSerializerOptions BatchJobPayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string NonProcessableBatchJobError =
        "Privacy-withdrawal batch job was no longer processable.";

    /// <summary>
    /// Atomically queues a token-authenticated withdrawal for one RefTest. A repeated request is
    /// accepted as already queued while its durable target has processable work; otherwise the
    /// existing incomplete batch is re-enqueued before the request is accepted.
    /// </summary>
    /// <param name="token">The participant's invitation or session credential.</param>
    /// <param name="cancellationToken">Token for cancelling the request.</param>
    /// <returns>
    /// True when work was newly queued, already processable, or safely re-enqueued; false when
    /// the token does not identify a participant record.
    /// </returns>
    public async Task<bool> RequestForParticipantAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        for (var concurrencyRetry = 0; ; concurrencyRetry++)
        {
            try
            {
                return await strategy.ExecuteAsync(cancellationToken, async retryToken =>
                {
                    // A retry must start from database state, not entities tracked by a transaction
                    // with an uncertain commit outcome.
                    context.ChangeTracker.Clear();
                    await using var transaction = await context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        retryToken);

                    var refTest = await context.RefTests
                        .FindByParticipantCredentialAsync(token, sessionTokenService, retryToken);
                    if (refTest is null)
                    {
                        await transaction.CommitAsync(retryToken);
                        return false;
                    }

                    // A pending target suppresses new work while its batch job is processable.
                    // Otherwise, recover that same batch instead of creating a duplicate target.
                    var now = DateTime.UtcNow;
                    var recovery = await EnsureProcessableTargetsAsync(
                        [refTest.Id],
                        now,
                        retryToken);

                    if (!recovery.ClaimedTargetIds.Contains(refTest.Id))
                        await QueueBatchAsync([refTest.Id], now, retryToken);

                    // The target snapshot and ID-only worker job commit together. Do not report
                    // acceptance until this transaction commits successfully.
                    await context.SaveChangesAsync(retryToken);
                    await transaction.CommitAsync(retryToken);
                    return true;
                });
            }
            catch (DbUpdateConcurrencyException) when (concurrencyRetry < 1)
            {
                // A concurrent request or scheduled sweep already recorded a replacement job.
                // Re-read its durable state and acknowledge the existing claim.
                context.ChangeTracker.Clear();
            }
        }
    }

    public async Task RequestAsync(string? email, CancellationToken cancellationToken)
    {
        var candidateEmail = email?.Trim();
        if (string.IsNullOrEmpty(candidateEmail)
            || candidateEmail.Length > 256
            || !new EmailAddressAttribute().IsValid(candidateEmail))
            return;

        var normalizedEmail = PrivacyWithdrawalChallenge.NormalizeEmail(candidateEmail);
        try
        {
            await EnsureEmailLookupKeysBackfilledAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Do not acknowledge a matching attempt if the legacy lookup keys could not be
            // verified and backfilled; no indexed matching or challenge creation has occurred.
            logger.LogError("Privacy-withdrawal lookup backfill failed; matching was not attempted.");
            throw;
        }

        try
        {
            var matchingRefTests = await FindMatchingRefTestsAsync(context, normalizedEmail, cancellationToken);
            if (matchingRefTests.Count == 0)
                return;

            var now = DateTime.UtcNow;
            var normalizedEmailHash = PrivacyWithdrawalChallenge.HashNormalizedEmail(normalizedEmail);
            var challenge = await context.PrivacyWithdrawalChallenges
                .SingleOrDefaultAsync(
                    candidate => candidate.NormalizedEmailHash == normalizedEmailHash,
                    cancellationToken);

            // The unique normalized-address index closes the race between this check and insert;
            // either way, no second challenge email is committed while one remains unexpired.
            if (challenge is not null && challenge.ExpiresAt > now)
                return;

            var challengeKey = TokenService.GenerateBase64Url(32);
            var protectedDeliveryKey = keyProtection.Protect(challengeKey);
            var recipientEmail = matchingRefTests[0].Email.Trim();
            var expiresAt = now.AddHours(configuration.PrivacyChallengeKeyLifetimeHours);

            if (challenge is null)
            {
                challenge = PrivacyWithdrawalChallenge.Create(
                    recipientEmail,
                    normalizedEmailHash,
                    challengeKey,
                    protectedDeliveryKey,
                    now,
                    expiresAt,
                    matchingRefTests.Count);
                context.PrivacyWithdrawalChallenges.Add(challenge);
            }
            else
            {
                challenge.Renew(
                    recipientEmail,
                    normalizedEmailHash,
                    challengeKey,
                    protectedDeliveryKey,
                    now,
                    expiresAt,
                    matchingRefTests.Count);
            }

            await jobEnqueueService.EnqueuePrivacyWithdrawalChallengeEmailAsync(
                new PrivacyWithdrawalChallengeEmailPayload(challenge.Id),
                saveChanges: false,
                unitOfWorkContext: context,
                cancellationToken: cancellationToken);
            await context.SaveChangesWithRetryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The public operation must not reveal whether an address matched or whether a
            // duplicate request won the unique-index race.
            logger.LogError("Privacy-withdrawal request could not be processed.");
        }
    }

    public async Task<int> ReconcileIncompleteBatchesAsync(CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        for (var concurrencyRetry = 0; ; concurrencyRetry++)
        {
            try
            {
                return await strategy.ExecuteAsync(cancellationToken, async retryToken =>
                {
                    context.ChangeTracker.Clear();
                    await using var transaction = await context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        retryToken);

                    var now = DateTime.UtcNow;
                    var dueTargetIds = await context.PrivacyWithdrawalBatchTargets
                        .Where(target => target.CompletedAt == null
                                         && target.RetryExhaustedAt == null
                                         && (target.AttemptCount >= PrivacyWithdrawalBatchTarget.MaximumAttempts
                                             || target.NextAttemptAt == null
                                             || target.NextAttemptAt <= now)
                                         && context.PrivacyWithdrawalBatches.Any(
                                             batch => batch.Id == target.BatchId && batch.CompletedAt == null))
                        .Select(target => target.RefTestId)
                        .Distinct()
                        .Take(ReconciliationBatchSize)
                        .ToListAsync(retryToken);

                    var recovery = await EnsureProcessableTargetsAsync(dueTargetIds, now, retryToken);
                    await CompleteTerminalBatchesAsync(now, retryToken);

                    if (context.ChangeTracker.HasChanges())
                        await context.SaveChangesAsync(retryToken);

                    await transaction.CommitAsync(retryToken);
                    return recovery.RequeuedBatchCount;
                });
            }
            catch (DbUpdateConcurrencyException) when (concurrencyRetry < 1)
            {
                // A concurrent participant request or worker won this batch; re-read it once.
                context.ChangeTracker.Clear();
            }
        }
    }

    public async Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken)
    {
        if (!IsValidChallengeKey(challengeKey))
            return false;

        try
        {
            var key = challengeKey!;
            var keyHash = PrivacyWithdrawalChallenge.HashKey(key);
            var hasPendingChallenge = await context.PrivacyWithdrawalChallenges
                .AsNoTracking()
                .AnyAsync(challenge => challenge.KeyHash == keyHash, cancellationToken);
            if (!hasPendingChallenge)
                return false;

            // Backfill before opening the serializable confirmation transaction. A failure is
            // caught below and the confirmation is not accepted.
            await EnsureEmailLookupKeysBackfilledAsync(cancellationToken);

            var strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(cancellationToken, async retryToken =>
            {
                // A retry must start from the database state, not entities left tracked by a
                // transaction whose commit outcome was uncertain.
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    retryToken);

                var challenge = await context.PrivacyWithdrawalChallenges
                    .SingleOrDefaultAsync(candidate => candidate.KeyHash == keyHash, retryToken);

                if (challenge is null)
                    return false;

                var now = DateTime.UtcNow;
                if (challenge.ExpiresAt <= now)
                {
                    if (challenge.ClearExpiredChallenge(now))
                        await context.SaveChangesAsync(retryToken);

                    await transaction.CommitAsync(retryToken);
                    return false;
                }

                var normalizedEmail = PrivacyWithdrawalChallenge.NormalizeEmail(challenge.Email);
                if (!challenge.TryConfirm(key, now))
                    return false;

                // The indexed digest narrows database candidates. Verify the complete normalized
                // address in application code so even a hypothetical digest collision cannot match.
                var matchingRefTests = await FindMatchingRefTestsAsync(context, normalizedEmail, retryToken);
                if (matchingRefTests.Count > 0)
                {
                    // Share active-claim detection and stale-batch recovery with the
                    // token-authenticated path so neither entry point can strand or duplicate work.
                    var processableTargetIds = await EnsureProcessableTargetsAsync(
                        matchingRefTests.Select(refTest => refTest.Id).ToArray(),
                        now,
                        retryToken);
                    matchingRefTests.RemoveAll(
                        refTest => processableTargetIds.ClaimedTargetIds.Contains(refTest.Id));
                }

                if (matchingRefTests.Count > 0)
                    await QueueBatchAsync(
                        matchingRefTests.Select(refTest => refTest.Id).ToArray(),
                        now,
                        retryToken);

                // The consumed challenge, batch, target snapshot, and ID-only outbox job share
                // this serializable transaction.
                await context.SaveChangesAsync(retryToken);
                await transaction.CommitAsync(retryToken);
                return true;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            // A competing confirmation consumed the challenge first; its batch and job are the
            // only ones that can commit.
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogError("Privacy-withdrawal confirmation could not be processed.");
            return false;
        }
    }

    private async Task QueueBatchAsync(
        IReadOnlyCollection<Guid> refTestIds,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        if (refTestIds.Count == 0)
            return;

        var batch = PrivacyWithdrawalBatch.Create(createdAt, refTestIds.Count);
        context.PrivacyWithdrawalBatches.Add(batch);

        foreach (var refTestId in refTestIds)
            context.PrivacyWithdrawalBatchTargets.Add(
                PrivacyWithdrawalBatchTarget.Create(batch.Id, refTestId));

        var jobId = await EnqueueBatchJobAsync(batch.Id, cancellationToken);
        batch.MarkJobEnqueued(jobId);
    }

    private async Task<BatchRecoveryResult> EnsureProcessableTargetsAsync(
        IReadOnlyCollection<Guid> requestedRefTestIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (requestedRefTestIds.Count == 0)
            return new([], 0);

        var requestedIds = requestedRefTestIds.ToList();
        var targets = await context.PrivacyWithdrawalBatchTargets
            .Where(target => requestedIds.Contains(target.RefTestId)
                            && context.PrivacyWithdrawalBatches.Any(
                                batch => batch.Id == target.BatchId && batch.CompletedAt == null))
            .Select(target => new { target.BatchId, target.RefTestId })
            .ToListAsync(cancellationToken);
        if (targets.Count == 0)
            return new([], 0);

        var batchIds = targets.Select(target => target.BatchId).Distinct().ToList();
        var pendingBatchTargets = await context.PrivacyWithdrawalBatchTargets
            .Where(target => batchIds.Contains(target.BatchId) && target.CompletedAt == null)
            .ToListAsync(cancellationToken);
        var targetsByBatch = pendingBatchTargets
            .GroupBy(target => target.BatchId)
            .ToDictionary(group => group.Key, group => group.ToList());

        // Link pre-migration active jobs once. New jobs carry a relational batch id, so later
        // sweeps query only jobs for the batches being reconciled rather than parsing the queue.
        await LinkLegacyBatchJobsAsync(cancellationToken);
        var batchJobs = await context.Jobs
            .Where(job => job.JobType == JobType.PrivacyWithdrawalBatch
                          && job.PrivacyWithdrawalBatchId != null
                          && batchIds.Contains(job.PrivacyWithdrawalBatchId.Value))
            .ToListAsync(cancellationToken);
        var jobsByBatch = new Dictionary<Guid, List<Job>>();
        foreach (var job in batchJobs)
        {
            var batchId = job.PrivacyWithdrawalBatchId!.Value;

            if (!jobsByBatch.TryGetValue(batchId, out var jobsForBatch))
            {
                jobsForBatch = [];
                jobsByBatch.Add(batchId, jobsForBatch);
            }

            jobsForBatch.Add(job);
        }

        var batchesById = await context.PrivacyWithdrawalBatches
            .Where(batch => batchIds.Contains(batch.Id) && batch.CompletedAt == null)
            .ToDictionaryAsync(batch => batch.Id, cancellationToken);
        var claimedTargetIds = targets.Select(target => target.RefTestId).ToHashSet();
        var requeuedBatchCount = 0;

        foreach (var batchId in batchIds)
        {
            var jobsForBatch = jobsByBatch.GetValueOrDefault(batchId) ?? [];
            var hasProcessableBatchWork = false;
            foreach (var batchJob in jobsForBatch)
            {
                if (HasProcessableBatchWork(batchJob, now))
                    hasProcessableBatchWork = true;
                else
                    MarkNonProcessableJobForCleanup(batchJob);
            }

            if (hasProcessableBatchWork)
                continue;

            var targetsForBatch = targetsByBatch.GetValueOrDefault(batchId) ?? [];
            var exhaustedCount = 0;
            foreach (var target in targetsForBatch)
            {
                if (target.MarkRetryLimitReached(now))
                    exhaustedCount++;
                else if (target.AttemptCount > 0
                         && target.NextAttemptAt is null
                         && target.RetryExhaustedAt is null
                         && target.RecordProcessingFailure(now)
                         && target.RetryExhaustedAt is not null)
                    exhaustedCount++;
            }

            if (exhaustedCount > 0)
            {
                logger.LogWarning(
                    "Privacy-withdrawal retry limit reached for {TargetCount} target(s).",
                    exhaustedCount);
            }

            var hasDueTarget = targetsForBatch.Any(target =>
                target.AttemptCount < PrivacyWithdrawalBatchTarget.MaximumAttempts
                && target.RetryExhaustedAt is null
                && (target.NextAttemptAt is null || target.NextAttemptAt <= now));
            if (!hasDueTarget)
                continue;

            var jobId = await EnqueueBatchJobAsync(batchId, cancellationToken);
            batchesById[batchId].MarkJobEnqueued(jobId);
            requeuedBatchCount++;
        }

        return new(claimedTargetIds, requeuedBatchCount);
    }

    private async Task LinkLegacyBatchJobsAsync(CancellationToken cancellationToken)
    {
        var legacyJobs = await context.Jobs
            .Where(job => job.JobType == JobType.PrivacyWithdrawalBatch
                          && job.PrivacyWithdrawalBatchId == null
                          && (job.Status == JobStatus.Pending
                              || job.Status == JobStatus.Processing
                              || job.CompletedAt == null
                              && (job.Status == JobStatus.Failed
                                  || job.Status == JobStatus.Completed
                                  || job.Status == JobStatus.Cancelled)))
            .ToListAsync(cancellationToken);

        foreach (var job in legacyJobs)
        {
            if (TryGetBatchId(job, out var batchId))
                job.AssociateWithPrivacyWithdrawalBatch(batchId);
            else
                MarkNonProcessableJobForCleanup(job);
        }
    }

    private async Task CompleteTerminalBatchesAsync(
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var terminalBatches = await context.PrivacyWithdrawalBatches
            .Where(batch => batch.CompletedAt == null
                            && batch.TargetCount == context.PrivacyWithdrawalBatchTargets.Count(
                                target => target.BatchId == batch.Id
                                          && (target.CompletedAt != null || target.RetryExhaustedAt != null))
                            && !context.PrivacyWithdrawalBatchTargets.Any(
                                target => target.BatchId == batch.Id
                                          && target.CompletedAt == null
                                          && target.RetryExhaustedAt == null))
            .OrderBy(batch => batch.CreatedAt)
            .Take(ReconciliationBatchSize)
            .ToListAsync(cancellationToken);

        if (terminalBatches.Count == 0)
            return;

        var terminalBatchIds = terminalBatches.Select(batch => batch.Id).ToArray();
        var exhaustedTargetCounts = await context.PrivacyWithdrawalBatchTargets
            .Where(target => terminalBatchIds.Contains(target.BatchId)
                             && target.CompletedAt == null
                             && target.RetryExhaustedAt != null)
            .GroupBy(target => target.BatchId)
            .Select(group => new { BatchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.BatchId, group => group.Count, cancellationToken);

        foreach (var batch in terminalBatches)
            batch.MarkCompleted(completedAt, exhaustedTargetCounts.GetValueOrDefault(batch.Id));
    }

    private bool HasProcessableBatchWork(Job job, DateTime now)
    {
        if (job.Status == JobStatus.Pending)
            return job.Attempts < backgroundJobConfiguration.MaxAttempts;

        if (job.Status != JobStatus.Processing)
            return false;

        // A live lease is still doing work even on the final allowed attempt. Once the lease has
        // expired, a job is recoverable only if the worker can claim another attempt.
        return (job.LockedUntil is { } lockedUntil && lockedUntil > now)
               || job.Attempts < backgroundJobConfiguration.MaxAttempts;
    }

    private async Task<Guid> EnqueueBatchJobAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (backgroundJobConfiguration.MaxAttempts < 1)
            throw new InvalidOperationException("BackgroundJobConfiguration.MaxAttempts must be positive.");

        return await jobEnqueueService.EnqueuePrivacyWithdrawalBatchAsync(
            new PrivacyWithdrawalBatchPayload(batchId),
            saveChanges: false,
            unitOfWorkContext: context,
            cancellationToken: cancellationToken);
    }

    private void MarkNonProcessableJobForCleanup(Job job)
    {
        if (job.Status is JobStatus.Pending or JobStatus.Processing
            || job.Status == JobStatus.Failed && job.CompletedAt is null)
        {
            job.MarkAsPermanentlyFailed(NonProcessableBatchJobError, timeProvider?.GetUtcNow().UtcDateTime);
        }
        else if (job.Status == JobStatus.Completed && job.CompletedAt is null)
        {
            job.MarkAsCompleted(timeProvider?.GetUtcNow().UtcDateTime);
        }
    }

    private static bool TryGetBatchId(Job job, out Guid batchId)
    {
        batchId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(job.Payload))
            return false;

        try
        {
            var payload = JsonSerializer.Deserialize<PrivacyWithdrawalBatchPayload>(
                job.Payload,
                BatchJobPayloadJsonOptions);
            if (payload is null || payload.BatchId == Guid.Empty)
                return false;

            batchId = payload.BatchId;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<List<MatchingRefTest>> FindMatchingRefTestsAsync(
        RefTestManagementContext dbContext,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var lookupKey = TokenService.HashBytes(normalizedEmail);
        var candidates = await PrivacyWithdrawalQueries.MatchingRefTestsByEmailLookupKey(
                dbContext.RefTests,
                lookupKey)
            .AsNoTracking()
            .Select(refTest => new MatchingRefTest(refTest.Id, refTest.Email))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(refTest => string.Equals(
                PrivacyWithdrawalChallenge.NormalizeEmail(refTest.Email),
                normalizedEmail,
                StringComparison.Ordinal))
            .ToList();
    }

    private async Task EnsureEmailLookupKeysBackfilledAsync(CancellationToken cancellationToken)
    {
        var concurrencyRetries = 0;
        while (true)
        {
            var pendingRefTests = await PrivacyWithdrawalQueries
                .EligibleRefTestsMissingEmailLookupKey(context.RefTests)
                .OrderBy(refTest => refTest.Id)
                .Take(EmailLookupBackfillBatchSize)
                .ToListAsync(cancellationToken);
            if (pendingRefTests.Count == 0)
                return;

            foreach (var refTest in pendingRefTests)
                refTest.BackfillEmailLookupKey();

            try
            {
                await context.SaveChangesWithRetryAsync(cancellationToken);
                foreach (var refTest in pendingRefTests)
                    context.Entry(refTest).State = EntityState.Detached;
                concurrencyRetries = 0;
            }
            catch (DbUpdateConcurrencyException)
            {
                // An email change or erasure won the optimistic-concurrency race. Discard the
                // stale values and reread current database state before deciding lookup is ready.
                foreach (var refTest in pendingRefTests)
                {
                    await context.Entry(refTest).ReloadAsync(cancellationToken);
                    context.Entry(refTest).State = EntityState.Detached;
                }
                if (++concurrencyRetries >= MaximumEmailLookupConcurrencyRetries)
                    throw;
            }
        }
    }

    private static bool IsValidChallengeKey(string? key) =>
        key is { Length: 43 }
        && key.All(character =>
            character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_');

    private sealed record BatchRecoveryResult(HashSet<Guid> ClaimedTargetIds, int RequeuedBatchCount);
    private sealed record MatchingRefTest(Guid Id, string Email);
}
