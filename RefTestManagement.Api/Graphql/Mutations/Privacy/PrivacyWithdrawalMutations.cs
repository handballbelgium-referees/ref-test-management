using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Abstractions;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;

/// <summary>Anonymous mailbox-verification operations for participant consent withdrawal.</summary>
[MutationType]
public static partial class PrivacyWithdrawalMutations
{
    /// <summary>
    /// Requests a one-time mailbox challenge. The acknowledgement is identical whether or not the
    /// address matches a participant record; only a matching address receives an email.
    /// </summary>
    public static async Task<PrivacyWithdrawalRequestAcknowledgement> RequestPrivacyWithdrawalAsync(
        PrivacyWithdrawalRequestInput input,
        [Service] IPrivacyWithdrawalRequestService requestService,
        [Service] IPrivacyChallengeRateLimiter rateLimiter,
        [Service] ICurrentClientAddress currentClientAddress,
        CancellationToken cancellationToken)
    {
        var clientAddress = currentClientAddress.Resolve();

        if (await rateLimiter.TryAcquireRequestAsync(clientAddress, cancellationToken))
            await requestService.RequestAsync(input.Email, cancellationToken);

        return new PrivacyWithdrawalRequestAcknowledgement(Acknowledged: true);
    }

    /// <summary>
    /// Consumes a one-time key only when explicitly invoked as a GraphQL mutation. Loading or
    /// prefetching an email link must not call this operation; valid keys are accepted only after
    /// the withdrawal service commits the confirmation and any required durable processing work.
    /// </summary>
    public static async Task<PrivacyWithdrawalConfirmationResult> ConfirmPrivacyWithdrawalAsync(
        string key,
        [Service] IPrivacyWithdrawalRequestService requestService,
        [Service] IPrivacyChallengeRateLimiter rateLimiter,
        [Service] ICurrentClientAddress currentClientAddress,
        CancellationToken cancellationToken)
    {
        var clientAddress = currentClientAddress.Resolve();

        if (!await rateLimiter.TryAcquireConfirmationAsync(clientAddress, cancellationToken))
            return new PrivacyWithdrawalConfirmationResult(Accepted: false);

        var accepted = await requestService.ConfirmAsync(key, cancellationToken);
        return new PrivacyWithdrawalConfirmationResult(accepted);
    }
}
