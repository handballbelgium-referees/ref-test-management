using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

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
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] ReportConfiguration reportConfig,
        [Service] ScoreConfiguration scoreConfig,
        [Service] ILoggerFactory loggerFactory,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RefTestEmailMutations");
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);
        var refTests = await context.RefTests
            .Include(s => s.Title)
            .Where(s => input.Ids.Contains(s.Id))
            .OrderBy(s => s.LastName)
            .ToListAsync(cancellationToken);

        if (refTests.Count == 0)
        {
            return new SendReportResult
            {
                Success = false,
                Message = "No RefTests found with the provided IDs",
                RefTestCount = 0
            };
        }

        var reportData = refTests.Select(s => new RefTestReportPayloadData(
            s.Id,
            s.Title?.Value ?? "Unknown",
            s.FirstName,
            s.LastName,
            s.StartedAt,
            s.CompletedAt,
            s.QuestionScore,
            s.QuestionTotal,
            s.AnswerScore,
            s.AnswerTotal,
            s.Percentage,
            s.Percentage >= scoreConfig.PassingPercentage,
            s.Language,
            s.Duration
        )).ToList();

        var recipients = reportConfig.RecipientEmails;

        if (recipients.Length == 0)
        {
            return new SendReportResult
            {
                Success = false,
                Message = "No recipient emails configured",
                RefTestCount = refTests.Count
            };
        }

        try
        {
            var reportPayload = new ReportEmailPayload(
                recipients,
                reportData,
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            );

            await jobEnqueueService.EnqueueReportEmailAsync(reportPayload, cancellationToken: cancellationToken);

            return new SendReportResult
            {
                Success = true,
                Message = $"Report job successfully enqueued for {recipients.Length} recipient(s)",
                RefTestCount = refTests.Count
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            MutationErrorHandling.LogMutationFailure(
                logger,
                ex,
                nameof(SendReportAsync),
                correlationId);

            return new SendReportResult
            {
                Success = false,
                Message = "Failed to enqueue report job. Please try again later.",
                RefTestCount = refTests.Count
            };
        }
    }

    private static User? ToUser(RefTestParticipant? participant) =>
        participant is null ? null : new User(participant.FirstName, participant.LastName, participant.Email);
}
