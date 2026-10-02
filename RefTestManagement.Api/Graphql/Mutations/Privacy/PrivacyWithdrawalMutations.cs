using Handball.Belgium.RefTestManagement.Api.Services;

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
        [Service] IPersonalDataExportRateLimiter rateLimiter,
        [Service] IClientIpResolver clientIpResolver,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var clientAddress = httpContextAccessor.HttpContext is { } httpContext
            ? clientIpResolver.Resolve(httpContext)
            : "unknown";

        if (rateLimiter.TryAcquireRequest(clientAddress))
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
        [Service] IPersonalDataExportRateLimiter rateLimiter,
        [Service] IClientIpResolver clientIpResolver,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var clientAddress = httpContextAccessor.HttpContext is { } httpContext
            ? clientIpResolver.Resolve(httpContext)
            : "unknown";

        if (!rateLimiter.TryAcquireConfirmation(clientAddress))
            return new PrivacyWithdrawalConfirmationResult(Accepted: false);

        var accepted = await requestService.ConfirmAsync(key, cancellationToken);
        return new PrivacyWithdrawalConfirmationResult(accepted);
    }
}
