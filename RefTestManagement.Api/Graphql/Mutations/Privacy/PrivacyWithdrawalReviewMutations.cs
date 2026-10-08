using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;

/// <summary>Protected operator actions for terminal privacy-withdrawal failures.</summary>
[MutationType]
public static partial class PrivacyWithdrawalReviewMutations
{
    /// <summary>
    /// Revalidates and acknowledges a terminal failed target. The action token is never included
    /// in the audit event.
    /// </summary>
    [Authorize(Policy = Permissions.PrivacyOperations.ReviewWithdrawals)]
    public static async Task<PrivacyWithdrawalTargetAcknowledgementResult>
        AcknowledgeFailedPrivacyWithdrawalTargetAsync(
            Guid actionToken,
            [Service] IPrivacyWithdrawalRequestService requestService,
            CancellationToken cancellationToken)
    {
        var acknowledged = await requestService.AcknowledgeFailedTargetAsync(
            actionToken,
            cancellationToken);

        return new PrivacyWithdrawalTargetAcknowledgementResult(
            acknowledged
                ? PrivacyWithdrawalTargetAcknowledgementStatus.Acknowledged
                : PrivacyWithdrawalTargetAcknowledgementStatus.NotAvailable);
    }
}

/// <summary>Whether the requested terminal failure was acknowledged or is no longer available.</summary>
public enum PrivacyWithdrawalTargetAcknowledgementStatus
{
    Acknowledged,
    NotAvailable
}

/// <summary>Result of an operator acknowledgement request.</summary>
public sealed record PrivacyWithdrawalTargetAcknowledgementResult(
    PrivacyWithdrawalTargetAcknowledgementStatus Status);
