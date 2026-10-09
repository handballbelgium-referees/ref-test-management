using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Deletion;

/// <summary>
/// RefTest deletion mutations
/// </summary>
[MutationType]
public static partial class RefTestDeletionMutations
{
    /// <summary>
    /// Delete RefTests. This is an explicit, authorized staff action that permanently deletes
    /// each RefTest record — unlike the public self-service "withdraw consent" flow, which only
    /// redacts personal data and keeps the record. Personal data is redacted from the RefTest's
    /// audit trail first (see <see cref="IRefTestPrivacyErasureService.EraseAsync"/>), so a
    /// staff delete leaves no personal data behind anywhere; the row itself is then removed
    /// (see <see cref="IRefTestPrivacyErasureService.EraseAndDeleteAsync"/>).
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="privacyErasureService"></param>
    /// <param name="subscriptionService"></param>
    /// <param name="jobEnqueueService">Used by the unit of work that loads the RefTests.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used for the correlation ID.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    [Authorize(Policy = Permissions.RefTests.Delete)]
    public static async Task<DeleteRefTestsResult> DeleteRefTestsAsync(
        DeleteRefTestsInput input,
        RefTestManagementContext context,
        [Service] IRefTestPrivacyErasureService privacyErasureService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] ILoggerFactory loggerFactory,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RefTestDeletionMutations");
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var outcome = await RefTestDeletionHandler.HandleAsync(
            input.Ids,
            refTest => refTest.ToDto(),
            new EfRefTestUnitOfWork(context, jobEnqueueService),
            privacyErasureService,
            subscriptionService,
            cancellationToken);

        var result = new DeleteRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullyDeleted = outcome.Deleted.Count,
            Failed = outcome.Failures.Count,
            DeletedRefTests = [.. outcome.Deleted],
            Errors = []
        };

        foreach (var failure in outcome.Failures)
        {
            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(DeleteRefTestsAsync), correlationId, failure.RefTestId);
            result.Errors.Add(new DeleteRefTestError
            {
                RefTestId = failure.RefTestId,
                ErrorMessage = MutationErrorHandling.GetUserSafeMessage(failure.Exception)
            });
        }

        return result;
    }
}

