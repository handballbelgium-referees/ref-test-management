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

namespace Handball.Belgium.RefTestManagement.Api.Services;

public interface IPrivacyWithdrawalRequestService
{
    Task RequestAsync(string? email, CancellationToken cancellationToken);
    Task<bool> RequestForParticipantAsync(string token, CancellationToken cancellationToken);
    Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken);
}

/// <summary>
/// Creates mailbox-verification challenges and atomically turns valid confirmations or
/// participant-credential requests into durable, batch-ID-only withdrawal jobs.
/// </summary>
public sealed class PrivacyWithdrawalRequestService(
    RefTestManagementContext context,
    IJobEnqueueService jobEnqueueService,
    IPersonalDataExportKeyProtection keyProtection,
    IRefTestSessionTokenService sessionTokenService,
    PrivacyChallengeConfiguration configuration,
    BackgroundJobConfiguration backgroundJobConfiguration,
    ILogger<PrivacyWithdrawalRequestService> logger) : IPrivacyWithdrawalRequestService
{
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

        return await strategy.ExecuteAsync(cancellationToken, async retryToken =>
        {
            // A retry must start from database state, not entities tracked by a transaction with
            // an uncertain commit outcome.
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

            // The serializable read protects both existing claims and the absence of one. A
            // pending target only suppresses new work while its batch job is processable; if its
            // job failed or disappeared, re-enqueue that same batch in this transaction.
            var now = DateTime.UtcNow;
            var processableTargetIds = await EnsureProcessableTargetsAsync(
                [refTest.Id],
                now,
                retryToken);

            if (!processableTargetIds.Contains(refTest.Id))
                await QueueBatchAsync([refTest.Id], now, retryToken);

            // The target snapshot and ID-only worker job commit together. Do not report acceptance
            // until this transaction commits successfully.
            await context.SaveChangesAsync(retryToken);
            await transaction.CommitAsync(retryToken);
            return true;
        });
    }

    public async Task RequestAsync(string? email, CancellationToken cancellationToken)
    {
        var candidateEmail = email?.Trim();
        if (string.IsNullOrEmpty(candidateEmail)
            || candidateEmail.Length > 256
            || !new EmailAddressAttribute().IsValid(candidateEmail))
            return;

        try
        {
            var normalizedEmail = PrivacyWithdrawalChallenge.NormalizeEmail(candidateEmail);
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

    public async Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken)
    {
        if (!IsValidChallengeKey(challengeKey))
            return false;

        try
        {
            var key = challengeKey!;
            var keyHash = PrivacyWithdrawalChallenge.HashKey(key);
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

                // Match in memory using the same invariant normalization for every provider.
                // Status is intentionally absent: every non-anonymized RefTest is eligible.
                var matchingRefTests = await FindMatchingRefTestsAsync(context, normalizedEmail, retryToken);
                if (matchingRefTests.Count > 0)
                {
                    // Share active-claim detection and stale-batch recovery with the
                    // token-authenticated path so neither entry point can strand or duplicate work.
                    var processableTargetIds = await EnsureProcessableTargetsAsync(
                        matchingRefTests.Select(refTest => refTest.Id).ToArray(),
                        now,
                        retryToken);
                    matchingRefTests.RemoveAll(refTest => processableTargetIds.Contains(refTest.Id));
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

        await EnqueueBatchJobAsync(batch.Id, cancellationToken);
    }

    private async Task<HashSet<Guid>> EnsureProcessableTargetsAsync(
        IReadOnlyCollection<Guid> requestedRefTestIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (requestedRefTestIds.Count == 0)
            return [];

        var requestedIds = requestedRefTestIds.ToArray();
        var targets = await context.PrivacyWithdrawalBatchTargets
            .Where(target => requestedIds.Contains(target.RefTestId)
                            && target.CompletedAt == null
                            && context.PrivacyWithdrawalBatches.Any(
                                batch => batch.Id == target.BatchId && batch.CompletedAt == null))
            .Select(target => new { target.BatchId, target.RefTestId })
            .ToListAsync(cancellationToken);
        if (targets.Count == 0)
            return [];

        var batchIds = targets.Select(target => target.BatchId).Distinct().ToArray();
        var pendingBatchTargets = await context.PrivacyWithdrawalBatchTargets
            .Where(target => batchIds.Contains(target.BatchId) && target.CompletedAt == null)
            .Select(target => new { target.BatchId, target.RefTestId })
            .ToListAsync(cancellationToken);
        var targetIdsByBatch = pendingBatchTargets
            .GroupBy(target => target.BatchId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(target => target.RefTestId).Distinct().ToArray());

        // Job payloads intentionally contain only a batch id and there is no batch/job FK. Load
        // the type-specific outbox rows and associate each payload without provider-specific JSON
        // queries.
        var batchJobs = await context.Jobs
            .Where(job => job.JobType == JobType.PrivacyWithdrawalBatch)
            .ToListAsync(cancellationToken);
        var jobsByBatch = new Dictionary<Guid, List<Job>>();
        foreach (var job in batchJobs)
        {
            if (!TryGetBatchId(job, out var batchId))
                continue;

            if (!jobsByBatch.TryGetValue(batchId, out var jobsForBatch))
            {
                jobsForBatch = [];
                jobsByBatch.Add(batchId, jobsForBatch);
            }

            jobsForBatch.Add(job);
        }

        var processableTargetIds = new HashSet<Guid>();
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

            // Exhausted processing rows are not picked up by BackgroundJobService cleanup. Mark
            // them terminal so the configured failed-job retention can remove them; failed,
            // completed, and missing rows are replaced by a fresh job for this same batch.
            if (hasProcessableBatchWork)
            {
                processableTargetIds.UnionWith(targetIdsByBatch[batchId]);
                continue;
            }

            await EnqueueBatchJobAsync(batchId, cancellationToken);
            processableTargetIds.UnionWith(targetIdsByBatch[batchId]);
        }

        return processableTargetIds;
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

    private async Task EnqueueBatchJobAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (backgroundJobConfiguration.MaxAttempts < 1)
            throw new InvalidOperationException("BackgroundJobConfiguration.MaxAttempts must be positive.");

        await jobEnqueueService.EnqueuePrivacyWithdrawalBatchAsync(
            new PrivacyWithdrawalBatchPayload(batchId),
            saveChanges: false,
            unitOfWorkContext: context,
            cancellationToken: cancellationToken);
    }

    private static void MarkNonProcessableJobForCleanup(Job job)
    {
        if (job.Status is JobStatus.Pending or JobStatus.Processing
            || job.Status == JobStatus.Failed && job.CompletedAt is null)
        {
            job.MarkAsPermanentlyFailed(NonProcessableBatchJobError);
        }
        else if (job.Status == JobStatus.Completed && job.CompletedAt is null)
        {
            job.MarkAsCompleted();
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
        var candidates = await PrivacyWithdrawalQueries.EligibleRefTests(dbContext.RefTests)
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

    private static bool IsValidChallengeKey(string? key) =>
        key is { Length: 43 }
        && key.All(character =>
            character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_');

    private sealed record MatchingRefTest(Guid Id, string Email);
}
