using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>Sends a withdrawal verification email using the challenge's protected retry state.</summary>
public sealed class PrivacyWithdrawalChallengeEmailJobHandler(
    RefTestManagementContext context,
    IEmailService emailService,
    IPersonalDataExportKeyProtection keyProtection,
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

        try
        {
            await emailService.SendPrivacyWithdrawalVerificationAsync(
                challenge.Email,
                key,
                challenge.ExpiresAt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (challenge.MarkChallengeEmailDeliveryFailed(DateTime.UtcNow))
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
