using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Sends a participant their result breakdown and records that it went out.
/// </summary>
public sealed class ResultEmailJobHandler(
    IEmailService emailService,
    IIhfRulesQuestionsService questionsService,
    RefTestManagementContext context,
    IRefTestSubscriptionService subscriptionService,
    ILogger<ResultEmailJobHandler> logger) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<ResultEmailPayload>(job, logger);

        ServiceLoggerMessages.LogSendingResultEmail(logger, payload.RefTestId);

        // Get the RefTest to retrieve all question IDs
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(r => r.Id == payload.RefTestId, cancellationToken);

        if (refTest == null)
        {
            throw new InvalidOperationException($"RefTest {payload.RefTestId} not found");
        }

        // Get ALL questions with correct answers (not just the wrong ones)
        var questionsWithCorrectAnswers = await questionsService.GetQuestionsByIdAsync(
            refTest.QuestionIds,
            includeNumber: true,
            includeIsCorrect: true,
            randomAnswerOrder: false,
            cancellationToken: cancellationToken);

        await emailService.SendRefTestResultsAsync(
            payload.RefTestId,
            payload.Name,
            payload.Email,
            payload.QuestionScore,
            payload.AnswerScore,
            payload.TotalQuestions,
            payload.AnswerTotal,
            payload.Percentage,
            payload.SelectedAnswerIds,
            payload.WrongQuestionIds,
            payload.WrongAnswerIds,
            questionsWithCorrectAnswers,
            scheduleEmail: false,
            cancellationToken); // Already scheduled via the job system

        // Mark the RefTest results as sent
        refTest.SendResults();
        await context.SaveChangesWithRetryAsync(cancellationToken);

        // Publish subscription event
        await subscriptionService.PublishResultSentAsync(
            refTest.Id,
            refTest.ResultsSentAt!.Value,
            cancellationToken);
    }
}
