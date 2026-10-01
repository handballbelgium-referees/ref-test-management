using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>Sends a verification email using the request record's protected retry state.</summary>
public sealed class PersonalDataExportChallengeEmailJobHandler(
    RefTestManagementContext context,
    IEmailService emailService,
    IPersonalDataExportKeyProtection keyProtection,
    ILogger<PersonalDataExportChallengeEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<PersonalDataExportChallengeEmailPayload>(job, logger);
        var request = await context.PersonalDataExportRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == payload.RequestId, cancellationToken);

        if (request is null || request.VerifiedAt is not null || request.ChallengeEmailSentAt is not null)
            return;

        var now = DateTime.UtcNow;
        if (request.ExpiresAt <= now)
        {
            if (request.ClearExpiredChallenge(now))
                await context.SaveChangesWithRetryAsync(cancellationToken);
            return;
        }

        var protectedKey = request.ProtectedDeliveryKey;
        if (string.IsNullOrEmpty(protectedKey))
        {
            request.ClearForPrivacyErasure();
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
            // A lost or invalid data-protection key cannot be repaired by a job retry. Clear the
            // recipient/challenge data and quarantine the job without persisting exception text.
            request.ClearForPrivacyErasure();
            await context.SaveChangesWithRetryAsync(cancellationToken);
            throw new JobPayloadException("The protected export challenge is no longer available.");
        }

        try
        {
            await emailService.SendPersonalDataExportVerificationAsync(
                request.Email,
                key,
                request.Locale,
                request.ExpiresAt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (request.MarkChallengeEmailDeliveryFailed(DateTime.UtcNow))
                await context.SaveChangesWithRetryAsync(cancellationToken);

            // Provider exceptions may include request content. The job queue persists exception
            // messages, so only a fixed, non-sensitive message is allowed past this boundary.
            throw new PersonalDataExportEmailDeliveryException();
        }

        now = DateTime.UtcNow;
        if (request.MarkChallengeEmailDelivered(now))
            await context.SaveChangesWithRetryAsync(cancellationToken);
        else if (request.ClearExpiredChallenge(now))
            await context.SaveChangesWithRetryAsync(cancellationToken);
    }
}

/// <summary>A retryable challenge-email failure with no recipient or key in its message.</summary>
public sealed class PersonalDataExportEmailDeliveryException()
    : Exception("Personal-data export verification email delivery failed.");
