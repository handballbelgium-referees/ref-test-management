using System.ComponentModel.DataAnnotations;
using System.Data;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
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
    Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken);
}

/// <summary>
/// Creates mailbox-verification challenges and atomically turns a valid confirmation into a
/// durable, batch-ID-only withdrawal job.
/// </summary>
public sealed class PrivacyWithdrawalRequestService(
    RefTestManagementContext context,
    IJobEnqueueService jobEnqueueService,
    IPersonalDataExportKeyProtection keyProtection,
    PersonalDataExportConfiguration configuration,
    ILogger<PrivacyWithdrawalRequestService> logger) : IPrivacyWithdrawalRequestService
{
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
                var batch = PrivacyWithdrawalBatch.Create(now, matchingRefTests.Count);
                context.PrivacyWithdrawalBatches.Add(batch);

                foreach (var refTest in matchingRefTests)
                    context.PrivacyWithdrawalBatchTargets.Add(
                        PrivacyWithdrawalBatchTarget.Create(batch.Id, refTest.Id));

                await jobEnqueueService.EnqueuePrivacyWithdrawalBatchAsync(
                    new PrivacyWithdrawalBatchPayload(batch.Id),
                    saveChanges: false,
                    unitOfWorkContext: context,
                    cancellationToken: retryToken);

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
