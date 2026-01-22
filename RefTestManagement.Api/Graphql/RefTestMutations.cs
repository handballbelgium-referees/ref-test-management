using Handball.Belgium.RefTestManagement.Api.Graphql.Models;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

/// <summary>
/// RefTest mutations
/// </summary>
[MutationType]
public static class RefTestMutations
{
    /// <summary>
    /// Start a RefTest
    /// </summary>
    /// <param name="token"></param>
    /// <param name="context"></param>
    /// <param name="configuration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="RefTestExpiredException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<RefTestExpiredException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> StartRefTestAsync(
        string token,
        RefTestManagementContext context,
        BackgroundServiceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(token);

        if (refTest.IsExpired(configuration.ExpirationIfNotStarted))
        {
            refTest.Expire();
            context.RefTests.Update(refTest);
            await context.SaveChangesAsync(cancellationToken);
            throw new RefTestExpiredException(token);
        }

        if (refTest.Status == RefTestStatus.InProgress)
            return refTest.ToDto();

        refTest.Start();
        context.RefTests.Update(refTest);
        await context.SaveChangesAsync(cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Save RefTest progress (current question and selected answers)
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> SaveRefTestProgressAsync(
        SaveRefTestProgressInput input,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == input.Token, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(input.Token);

        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        refTest.SaveProgress(input.CurrentQuestionIndex, input.SelectedAnswerIds, input.Language);
        context.RefTests.Update(refTest);
        await context.SaveChangesAsync(cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Complete a RefTest and (optional) send results email
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="emailConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> CompleteRefTestAsync(
        CompleteRefTestInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] EmailConfiguration emailConfiguration,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == input.Token, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(input.Token);

        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        // Use existing score calculation logic
        var scoreResult = await ihfRulesQuestionsService.CalculateScoreAsync(
            refTest.QuestionIds,
            input.SelectedAnswerIds,
            cancellationToken
        );

        // Complete RefTest with calculated results
        refTest.Complete(
            scoreResult.QuestionScore,
            scoreResult.AnswerScore,
            scoreResult.AnswerTotal,
            scoreResult.Percentage,
            input.SelectedAnswerIds,
            scoreResult.WrongQuestionIds,
            scoreResult.WrongAnswerIds,
            input.Language
        );

        context.RefTests.Update(refTest);
        await context.SaveChangesAsync(cancellationToken);

        if (!refTest.SendResultsAutomatically)
            return refTest.ToDto();

        // Enqueue result email job
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

        DateTime? scheduledAt = emailConfiguration.ScheduledDelayMinutes > 0
            ? DateTime.UtcNow.AddMinutes(emailConfiguration.ScheduledDelayMinutes)
            : null;
        await jobEnqueueService.EnqueueResultEmailAsync(payload, scheduledAt, cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Create multiple RefTests for multiple users
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static async Task<BulkRefTestsResult> CreateBulkRefTestsAsync(
        CreateBulkRefTestsInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var result = new BulkRefTestsResult
        {
            TotalRequested = input.Users.Count
        };

        var createdRefTests = new List<RefTest>();

        // Get questionIds from question numbers if specified
        List<string> specifiedQuestionIds = [];
        if (input.SpecificQuestionNumbers is not null)
        {
            specifiedQuestionIds = await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(
                input.SpecificQuestionNumbers,
                cancellationToken);
        }
        else if (input.SpecificQuestionNumbers is null && !input.RandomQuestionsForEachUser)
        {
            specifiedQuestionIds =
                await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions, cancellationToken);
        }

        Guid titleId;

        switch (input.Title.Id)
        {
            case not null:
                titleId = input.Title.Id.Value;
                break;
            case null when input.Title.Name is not null:
            {
                var title = RefTestTitle.Create(input.Title.Name);
                context.RefTestTitles.Add(title);
                await context.SaveChangesAsync(cancellationToken);

                titleId = title.Id;
                break;
            }
            default:
                throw new ArgumentException("Either Title.Id or Title.Name must be provided.");
        }

        foreach (var user in input.Users)
        {
            try
            {
                // If no specific question numbers were specified, get random questions
                var questionIds = specifiedQuestionIds;

                if (input.RandomQuestionsForEachUser)
                {
                    questionIds = await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions,
                        cancellationToken);
                }

                var refTest = RefTest.Create(
                    titleId,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    input.NumberOfQuestions,
                    input.MaxTimeInMinutes,
                    questionIds,
                    input.SendAutomatedInvitations,
                    input.SendAutomatedResults
                );

                createdRefTests.Add(refTest);
                result.SuccessfullyCreated++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new BulkCreationError
                {
                    User = user,
                    ErrorMessage = ex.Message
                });
            }
        }

        // Save all valid RefTests to a database
        if (createdRefTests.Count == 0)
            return result;

        if (!input.SendAutomatedInvitations)
        {
            context.RefTests.AddRange(createdRefTests);
            await context.SaveChangesAsync(cancellationToken);

            return result;
        }

        // Send invitation emails to all participants
        foreach (var refTest in createdRefTests)
        {
            try
            {
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

                result.CreatedRefTests.Add(refTest.ToDto());
            }
            catch (Exception ex)
            {
                // Email job enqueue failed, but the RefTest was created
                // Log the error but don't fail the entire operation
                result.Errors.Add(new BulkCreationError
                {
                    User = new User(refTest.FirstName, refTest.LastName, refTest.Email),
                    ErrorMessage = $"RefTest created but email job enqueue failed: {ex.Message}"
                });
            }
        }

        context.RefTests.AddRange(createdRefTests);
        await context.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Send RefTest invitation emails
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
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
    [Authorize]
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
    /// Delete a RefTest
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    [Authorize]
    public static async Task<DeleteRefTestsResult> DeleteRefTestsAsync(
        DeleteRefTestsInput input,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var refTests = await context.RefTests
            .Where(s => input.Ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var result = new DeleteRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            DeletedRefTests = [],
            Errors = []
        };

        foreach (var id in input.Ids)
        {
            var refTest = refTests.FirstOrDefault(x => x.Id == id);

            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id.ToString());

                context.RefTests.Remove(refTest);
                result.SuccessfullyDeleted++;
                result.DeletedRefTests.Add(refTest.ToDto());
            }
            catch (Exception e)
            {
                result.Failed++;
                result.Errors.Add(new DeleteRefTestError
                {
                    RefTestId = id,
                    ErrorMessage = e.Message
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Generate and email a report of RefTests
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="reportConfig"></param>
    /// <param name="scoreConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static async Task<GenerateReportResult> GenerateRefTestsReportAsync(
        GenerateRefTestsReportInput input,
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
            return new GenerateReportResult
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
            return new GenerateReportResult
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

            return new GenerateReportResult
            {
                Success = true,
                Message = $"Report job successfully enqueued for {recipients.Length} recipient(s)",
                RefTestCount = refTests.Count
            };
        }
        catch (Exception ex)
        {
            return new GenerateReportResult
            {
                Success = false,
                Message = $"Failed to enqueue report job: {ex.Message}",
                RefTestCount = refTests.Count
            };
        }
    }
}