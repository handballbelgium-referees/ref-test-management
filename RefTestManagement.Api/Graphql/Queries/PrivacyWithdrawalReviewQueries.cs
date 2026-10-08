using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Queries;

/// <summary>Protected operator queries for terminal privacy-withdrawal failures.</summary>
[QueryType]
public static partial class PrivacyWithdrawalReviewQueries
{
    /// <summary>
    /// Gets terminal failed targets with only an opaque acknowledgement token and sanitized
    /// failure category.
    /// </summary>
    [Authorize(Policy = Permissions.PrivacyOperations.ReviewWithdrawals)]
    [UsePaging(IncludeTotalCount = false)]
    public static IQueryable<FailedPrivacyWithdrawalTarget> GetFailedPrivacyWithdrawalTargets(
        RefTestManagementContext context) =>
        context.PrivacyWithdrawalBatchTargets
            .AsNoTracking()
            .Where(target => target.RetryExhaustedAt != null
                             && target.CompletedAt == null
                             && (target.FailureCode == PrivacyWithdrawalTargetFailureCode.ProcessingFailed
                                 || target.FailureCode == PrivacyWithdrawalTargetFailureCode.AttemptLimitReached)
                             && context.PrivacyWithdrawalBatches.Any(
                                 batch => batch.Id == target.BatchId && batch.CompletedAt != null))
            .OrderBy(target => target.Id)
            .Select(target => new FailedPrivacyWithdrawalTarget(
                target.Id,
                target.FailureCode!.Value));
}

/// <summary>Minimal review data for one terminal failed withdrawal target.</summary>
public sealed record FailedPrivacyWithdrawalTarget(
    Guid ActionToken,
    PrivacyWithdrawalTargetFailureCode FailureCategory);
