using System.ComponentModel.DataAnnotations;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Security;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public interface IPersonalDataExportRequestService
{
    Task RequestAsync(string? email, CancellationToken cancellationToken);
    Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken);
}

/// <summary>Creates and atomically consumes public personal-data export verification challenges.</summary>
public sealed class PersonalDataExportRequestService(
    RefTestManagementContext context,
    IJobEnqueueService jobEnqueueService,
    IPersonalDataExportKeyProtection keyProtection,
    PrivacyChallengeConfiguration configuration,
    ILogger<PersonalDataExportRequestService> logger) : IPersonalDataExportRequestService
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
            var normalizedEmail = candidateEmail.ToUpperInvariant();
            var storedEmail = await context.RefTests
                .Where(refTest => !refTest.IsAnonymized && refTest.Email.ToUpper() == normalizedEmail)
                .Select(refTest => refTest.Email)
                .FirstOrDefaultAsync(cancellationToken);

            if (storedEmail is null)
                return;

            var now = DateTime.UtcNow;
            var key = TokenService.GenerateBase64Url(32);
            var request = PersonalDataExportRequest.Create(
                storedEmail,
                key,
                keyProtection.Protect(key),
                now,
                now.AddHours(configuration.PrivacyChallengeKeyLifetimeHours));

            context.PersonalDataExportRequests.Add(request);
            await jobEnqueueService.EnqueuePersonalDataExportChallengeEmailAsync(
                new PersonalDataExportChallengeEmailPayload(request.Id),
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
            // This public operation must not turn a matching address into a distinguishable
            // GraphQL error. The static message contains no participant data or challenge key.
            logger.LogError("Personal-data export request could not be processed.");
        }
    }

    public async Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken)
    {
        if (!IsValidChallengeKey(challengeKey))
            return false;

        try
        {
            var key = challengeKey!;
            var keyHash = PersonalDataExportRequest.HashKey(key);
            var request = await context.PersonalDataExportRequests
                .SingleOrDefaultAsync(candidate => candidate.KeyHash == keyHash, cancellationToken);

            if (request is null)
                return false;

            var now = DateTime.UtcNow;
            if (request.ExpiresAt <= now)
            {
                if (request.ClearExpiredChallenge(now))
                    await context.SaveChangesWithRetryAsync(cancellationToken);
                return false;
            }

            if (!request.TryConfirm(key, now))
                return false;

            // The verification transition and its ID-only delivery job are one outbox unit of
            // work: a confirmed request can never be committed without a job to deliver it.
            await jobEnqueueService.EnqueuePersonalDataExportDeliveryEmailAsync(
                new PersonalDataExportDeliveryEmailPayload(request.Id),
                saveChanges: false,
                unitOfWorkContext: context,
                cancellationToken: cancellationToken);
            await context.SaveChangesWithRetryAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another confirmation consumed the challenge first. It is single-use even under
            // concurrent POSTs, and the public response does not identify which request won.
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogError("Personal-data export confirmation could not be processed.");
            return false;
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
}
