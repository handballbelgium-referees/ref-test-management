using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;

/// <summary>
/// RefTest email mutations (send invitations, results, and reports)
/// </summary>
[MutationType]
public static partial class RefTestEmailMutations
{
    /// <summary>
    /// Send RefTest invitation emails
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to send invitations for.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for accessing the current HTTP context.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.SendInvitations)]
    public static async Task<SendInvitationsResult> SendInvitationsAsync(
        SendInvitationsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] ILoggerFactory loggerFactory,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RefTestEmailMutations");
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var outcome = await RefTestEmailHandler.SendInvitationsAsync(
            input.Ids, new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);

        var result = new SendInvitationsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullySent = outcome.Sent.Count,
            Failed = outcome.Failures.Count,
            SentRefTests = [.. outcome.Sent.Select(refTest => refTest.ToDto())],
            Errors = []
        };

        // Failures are reported per RefTest and never fail the whole operation.
        foreach (var failure in outcome.Failures)
        {
            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(SendInvitationsAsync), correlationId, failure.RefTestId);
            result.Errors.Add(new SendInvitationError
            {
                RefTestId = failure.RefTestId,
                User = ToUser(failure.Participant),
                ErrorMessage = "Failed to send invitation email."
            });
        }

        return result;
    }

    /// <summary>
    /// Send RefTest results emails
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to send results for.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for accessing the current HTTP context.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.SendResults)]
    public static async Task<SendResultsResult> SendResultsAsync(
        SendResultsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] ILoggerFactory loggerFactory,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RefTestEmailMutations");
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var outcome = await RefTestEmailHandler.SendResultsAsync(
            input.Ids, new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);

        var result = new SendResultsResult
        {
            TotalRequested = input.Ids.Count,
            SuccessfullySent = outcome.Sent.Count,
            Failed = outcome.Failures.Count,
            SentRefTests = [.. outcome.Sent.Select(refTest => refTest.ToDto())],
            Errors = []
        };

        // Failures are reported per RefTest and never fail the whole operation.
        foreach (var failure in outcome.Failures)
        {
            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(SendResultsAsync), correlationId, failure.RefTestId);
            result.Errors.Add(new SendResultError
            {
                RefTestId = failure.RefTestId,
                User = ToUser(failure.Participant),
                ErrorMessage = "Failed to send result email."
            });
        }

        return result;
    }

    /// <summary>
    /// Send RefTest report email
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to send the report for.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="reportConfig">The configuration for the report email.</param>
    /// <param name="scoreConfig">The configuration for scoring the RefTests.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for accessing the current HTTP context.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.SendReport)]
    public static async Task<SendReportResult> SendReportAsync(
        SendReportInput input,
        [Service] IRefTestReportDataSource reportDataSource,
        [Service] IReportEmailJobQueue reportEmailJobQueue,
        [Service] ReportConfiguration reportConfig,
        [Service] ScoreConfiguration scoreConfig,
        [Service] ILoggerFactory loggerFactory,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var logger = loggerFactory.CreateLogger("RefTestEmailMutations");
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);
        var outcome = await new SendRefTestReportHandler(
                reportDataSource,
                reportEmailJobQueue,
                reportConfig,
                scoreConfig,
                timeProvider ?? TimeProvider.System)
            .HandleAsync(input.Ids, cancellationToken);

        if (outcome.Failure == SendRefTestReportFailure.EnqueueFailed)
        {
            var exception = outcome.Exception
                ?? throw new InvalidOperationException("Report enqueue failure did not include its exception.");
            MutationErrorHandling.LogMutationFailure(
                logger,
                exception,
                nameof(SendReportAsync),
                correlationId);
        }

        return outcome.Failure switch
        {
            SendRefTestReportFailure.NoRefTests => new SendReportResult
            {
                Success = false,
                Message = "No RefTests found with the provided IDs",
                RefTestCount = 0
            },
            SendRefTestReportFailure.NoRecipients => new SendReportResult
            {
                Success = false,
                Message = "No recipient emails configured",
                RefTestCount = outcome.RefTestCount
            },
            SendRefTestReportFailure.EnqueueFailed => new SendReportResult
            {
                Success = false,
                Message = "Failed to enqueue report job. Please try again later.",
                RefTestCount = outcome.RefTestCount
            },
            SendRefTestReportFailure.None => new SendReportResult
            {
                Success = true,
                Message = $"Report job successfully enqueued for {outcome.RecipientCount} recipient(s)",
                RefTestCount = outcome.RefTestCount
            },
            _ => throw new InvalidOperationException($"Unhandled report request outcome '{outcome.Failure}'.")
        };
    }

    private static User? ToUser(RefTestParticipant? participant) =>
        participant is null ? null : new User(participant.FirstName, participant.LastName, participant.Email);
}
