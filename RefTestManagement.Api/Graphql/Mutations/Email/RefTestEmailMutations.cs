using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Permissions;
using Handball.Belgium.RefTestManagement.AuditLog;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;

/// <summary>
/// RefTest email mutations (send invitations, results, and reports)
/// </summary>
[MutationType]
public static class RefTestEmailMutations
{
    /// <summary>
    /// Send RefTest invitation emails
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize(Policy = Permission.RefTests.SendInvitations)]
    [AuditAction(AuditLogAction.RefTest.SendInvitations)]
    public static async Task<SendInvitationsResult> SendInvitationsAsync(
        SendInvitationsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTests = await context.RefTests
            .Where(s => input.Ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var result = new SendInvitationsResult
        {
            TotalRequested = input.Ids.Count,
            SentRefTests = [],
            Errors = []
        };

        foreach (var id in input.Ids)
        {
            var refTest = refTests.FirstOrDefault(x => x.Id == id);

            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id.ToString());

                if (refTest.Status != RefTestStatus.Pending)
                    throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.Pending);

                var invitationPayload = new InvitationEmailPayload(
                    refTest.Id,
                    refTest.FullName,
                    refTest.Email,
                    refTest.Token,
                    refTest.NumberOfQuestions,
                    refTest.MaxTimeInMinutes
                );

                await jobEnqueueService.EnqueueInvitationEmailAsync(invitationPayload,
                    cancellationToken: cancellationToken);

                result.SentRefTests.Add(refTest.ToDto());
                result.SuccessfullySent++;
            }
            catch (Exception e)
            {
                result.Failed++;

                // Email failed, or RefTest was not found, or RefTest is not pending
                // Log the error but don't fail the entire operation
                result.Errors.Add(new SendInvitationError
                {
                    RefTestId = id,
                    User = refTest is null ? null : new User(refTest.FirstName, refTest.LastName, refTest.Email),
                    ErrorMessage = e.Message
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Send RefTest results emails
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permission.RefTests.SendResults)]
    [AuditAction(AuditLogAction.RefTest.SendResults)]
    public static async Task<SendResultsResult> SendResultsAsync(
        SendResultsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTests = await context.RefTests
            .Where(s => input.Ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var result = new SendResultsResult
        {
            TotalRequested = input.Ids.Count,
            SentRefTests = [],
            Errors = []
        };

        foreach (var id in input.Ids)
        {
            var refTest = refTests.FirstOrDefault(x => x.Id == id);

            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id.ToString());

                if (refTest.Status != RefTestStatus.Completed)
                    throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.Completed);

                var payload = new ResultEmailPayload(
                    refTest.Id,
                    refTest.FullName,
                    refTest.Email,
                    refTest.QuestionScore ?? 0,
                    refTest.AnswerScore ?? 0,
                    refTest.QuestionTotal,
                    refTest.AnswerTotal ?? 0,
                    refTest.Percentage ?? 0,
                    refTest.SelectedAnswerIds,
                    refTest.WrongQuestionIds,
                    refTest.WrongAnswerIds
                );

                await jobEnqueueService.EnqueueResultEmailAsync(payload, cancellationToken: cancellationToken);

                result.SentRefTests.Add(refTest.ToDto());
                result.SuccessfullySent++;
            }
            catch (Exception e)
            {
                result.Failed++;

                // Email failed, or RefTest was not found, or RefTest is not pending
                // Log the error but don't fail the entire operation
                result.Errors.Add(new SendResultError
                {
                    RefTestId = id,
                    User = refTest is null ? null : new User(refTest.FirstName, refTest.LastName, refTest.Email),
                    ErrorMessage = e.Message
                });
            }
        }

        return result;
    }
    
    /// <summary>
    /// Send RefTest report email
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="reportConfig"></param>
    /// <param name="scoreConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize(Policy = Permission.RefTests.SendReport)]
    [AuditAction(AuditLogAction.RefTest.SendReport)]
    public static async Task<SendReportResult> SendReportAsync(
        SendReportInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] ReportConfiguration reportConfig,
        [Service] ScoreConfiguration scoreConfig,
        CancellationToken cancellationToken)
    {
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
        catch (Exception ex)
        {
            return new SendReportResult
            {
                Success = false,
                Message = $"Failed to enqueue report job: {ex.Message}",
                RefTestCount = refTests.Count
            };
        }
    }
}

