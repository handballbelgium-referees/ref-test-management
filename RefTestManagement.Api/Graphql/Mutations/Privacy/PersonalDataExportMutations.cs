using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Abstractions;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;

/// <summary>Public mailbox-verification operations for a future personal-data export.</summary>
[MutationType]
public static partial class PersonalDataExportMutations
{
    /// <summary>
    /// Requests mailbox verification. The response does not reveal whether the address belongs
    /// to a participant; only a matching address receives a challenge email.
    /// </summary>
    public static async Task<PersonalDataExportRequestAcknowledgement> RequestPersonalDataExportAsync(
        PersonalDataExportRequestInput input,
        [Service] IPersonalDataExportRequestService requestService,
        [Service] IPrivacyChallengeRateLimiter rateLimiter,
        [Service] ICurrentClientAddress currentClientAddress,
        CancellationToken cancellationToken)
    {
        var clientAddress = currentClientAddress.Resolve();

        if (await rateLimiter.TryAcquireRequestAsync(clientAddress, cancellationToken))
            await requestService.RequestAsync(input.Email, cancellationToken);

        return new PersonalDataExportRequestAcknowledgement(Acknowledged: true);
    }

    /// <summary>
    /// Consumes a single-use confirmation key supplied only after the participant explicitly
    /// clicks Confirm on the page opened from the email link.
    /// </summary>
    public static async Task<PersonalDataExportConfirmationResult> ConfirmPersonalDataExportAsync(
        string key,
        [Service] IPersonalDataExportRequestService requestService,
        [Service] IPrivacyChallengeRateLimiter rateLimiter,
        [Service] ICurrentClientAddress currentClientAddress,
        CancellationToken cancellationToken)
    {
        var clientAddress = currentClientAddress.Resolve();

        if (!await rateLimiter.TryAcquireConfirmationAsync(clientAddress, cancellationToken))
            return new PersonalDataExportConfirmationResult(Confirmed: false);

        var confirmed = await requestService.ConfirmAsync(key, cancellationToken);
        return new PersonalDataExportConfirmationResult(confirmed);
    }
}
