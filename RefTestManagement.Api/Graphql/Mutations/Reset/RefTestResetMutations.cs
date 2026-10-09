using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

/// <summary>
/// RefTest reset mutations
/// </summary>
[MutationType]
public static partial class RefTestResetMutations
{
    /// <summary>
    /// Reset one or more RefTests to allow retake. Soft reset preserves the audit trail, hard reset clears everything.
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to reset and the reset type.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing and canceling job notifications.</param>
    /// <param name="subscriptionService">Service for managing RefTest subscriptions.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for accessing the current HTTP context.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Reset)]
    public static async Task<ResetRefTestsResult> ResetRefTestsAsync(
        ResetRefTestsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestResetMutations));
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);
        var result = new ResetRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            ResetRefTests = [],
            Errors = []
        };

        var outcome = await new ResetRefTestsHandler(subscriptionService, timeProvider ?? TimeProvider.System)
            .HandleAsync(input.Ids, input.ResetType, input.RegenerateToken,
                new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);

        var errors = new List<ResetRefTestsError>();
        foreach (var failure in outcome.Failures)
        {
            errors.Add(new ResetRefTestsError
            {
                RefTestId = failure.RefTestId,
                ErrorMessage = MutationErrorHandling.GetUserSafeMessage(failure.Exception)
            });
            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(ResetRefTestsAsync), correlationId, failure.RefTestId);
        }

        result.ResetRefTests.AddRange(outcome.Reset.Select(item => item.RefTest.ToDto()));
        return result with
        {
            SuccessfullyReset = outcome.Reset.Count,
            Failed = input.Ids.Count - outcome.Reset.Count,
            Errors = errors
        };
    }
    /// <summary>
    /// Revive one or more expired RefTests by resetting them to Pending status with a new token and fresh expiration timer
    /// </summary>
    /// <param name="ids">The IDs of the RefTests to revive.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing and canceling job notifications.</param>
    /// <param name="subscriptionService">Service for managing RefTest subscriptions.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for accessing the current HTTP context.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Revive)]
    public static async Task<ReviveRefTestsResult> ReviveRefTestsAsync(
        [ID<RefTestDto>] List<Guid> ids,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestResetMutations));
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);
        var result = new ReviveRefTestsResult
        {
            TotalRequested = ids.Count,
            RevivedRefTests = [],
            Errors = []
        };

        var outcome = await new ReviveRefTestsHandler(subscriptionService, timeProvider ?? TimeProvider.System)
            .HandleAsync(ids, new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);

        var errors = new List<ReviveRefTestsError>();
        foreach (var failure in outcome.Failures)
        {
            errors.Add(new ReviveRefTestsError
            {
                RefTestId = failure.RefTestId,
                ErrorMessage = MutationErrorHandling.GetUserSafeMessage(failure.Exception)
            });
            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(ReviveRefTestsAsync), correlationId, failure.RefTestId);
        }

        result.RevivedRefTests.AddRange(outcome.Revived.Select(refTest => refTest.ToDto()));
        return result with
        {
            SuccessfullyRevived = outcome.Revived.Count,
            Failed = outcome.Failures.Count,
            Errors = errors
        };
    }
}