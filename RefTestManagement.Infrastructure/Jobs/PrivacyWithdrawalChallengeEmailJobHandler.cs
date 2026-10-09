using Handball.Belgium.RefTestManagement.Infrastructure.Privacy;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Jobs;

/// <summary>Sends a withdrawal verification email using the challenge's protected retry state.</summary>
public sealed class PrivacyWithdrawalChallengeEmailJobHandler(
    RefTestManagementContext context,
    IEmailService emailService,
    IPersonalDataExportKeyProtection keyProtection,
    BackgroundJobConfiguration jobConfiguration,
    ILogger<PrivacyWithdrawalChallengeEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<PrivacyWithdrawalChallengeEmailPayload>(job, logger);
        var challenge = await context.PrivacyWithdrawalChallenges
            .SingleOrDefaultAsync(candidate => candidate.Id == payload.ChallengeId, cancellationToken);

        if (challenge is null || challenge.VerifiedAt is not null || challenge.ChallengeEmailSentAt is not null)
            return;

        var now = DateTime.UtcNow;
        if (challenge.ExpiresAt <= now)
        {
            if (challenge.ClearExpiredChallenge(now))
                await context.SaveChangesWithRetryAsync(cancellationToken);
            return;
        }

        var protectedKey = challenge.ProtectedDeliveryKey;
        if (string.IsNullOrEmpty(protectedKey))
        {
            challenge.ClearForPrivacyErasure();
            await context.SaveChangesWithRetryAsync(cancellationToken);
            return;
        }

        string key;
        try
        {
            key = keyProtection.Unprotect(protectedKey);
        }
        catch (Exception)
        {
            // A lost protection key cannot be repaired by retrying. Clear challenge PII and
            // quarantine the job without persisting the protected value or exception text.
            challenge.ClearForPrivacyErasure();
            await context.SaveChangesWithRetryAsync(cancellationToken);
            throw new JobPayloadException("The protected withdrawal challenge is no longer available.");
        }

        var recipientEmail = challenge.Email;
        var expiresAt = challenge.ExpiresAt;
        try
        {
            var wasSent = await emailService.SendPrivacyWithdrawalVerificationAsync(
                recipientEmail,
                key,
                expiresAt,
                finalCheckCancellationToken => context.PrivacyWithdrawalChallenges
                    .AsNoTracking()
                    .AnyAsync(
                        candidate => candidate.Id == payload.ChallengeId
                                     && candidate.VerifiedAt == null
                                     && candidate.ExpiresAt > DateTime.UtcNow
                                     && candidate.Email == recipientEmail
                                     && candidate.KeyHash != null
                                     && candidate.ProtectedDeliveryKey == protectedKey
                                     && candidate.ChallengeEmailSentAt == null,
                        finalCheckCancellationToken),
                cancellationToken);
            if (!wasSent)
                return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // ProcessJobAsync applies MarkAsFailed after this handler throws, so this run is
            // job.Attempts + 1. Keep the current key for retries, but expire it on the final run.
            var isFinalAttempt = job.Attempts + 1 >= jobConfiguration.MaxAttempts;
            if (challenge.MarkChallengeEmailDeliveryFailed(DateTime.UtcNow, isFinalAttempt))
                await context.SaveChangesWithRetryAsync(cancellationToken);

            // Provider failures can echo the recipient or the one-time key, so only this fixed
            // message may be persisted on the retryable job.
            throw new PrivacyWithdrawalEmailDeliveryException();
        }

        now = DateTime.UtcNow;
        if (challenge.MarkChallengeEmailDelivered(now))
            await context.SaveChangesWithRetryAsync(cancellationToken);
        else if (challenge.ClearExpiredChallenge(now))
            await context.SaveChangesWithRetryAsync(cancellationToken);
    }
}

/// <summary>A retryable withdrawal-email failure without recipient or challenge-key details.</summary>
public sealed class PrivacyWithdrawalEmailDeliveryException()
    : Exception("Privacy-withdrawal verification email delivery failed.");
