using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;

/// <summary>
/// RefTest lifecycle mutations (start, save progress, complete)
/// </summary>
[MutationType]
public static class RefTestLifecycleMutations
{
    /// <summary>
    /// Start a RefTest
    /// </summary>
    /// <param name="token"></param>
    /// <param name="context"></param>
    /// <param name="configuration"></param>
    /// <param name="subscriptionService"></param>
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
        [Service] RefTestExpirationConfiguration configuration,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(token);

        if (refTest.IsExpired(configuration.ExpirationIfNotStarted))
        {
            refTest.Expire();
            await context.SaveChangesAsync(cancellationToken);
            
            // Publish subscription event
            await subscriptionService.PublishRefTestExpiredAsync(
                refTest.Id,
                refTest.Status,
                DateTime.UtcNow,
                cancellationToken);
            
            throw new RefTestExpiredException(token);
        }

        if (refTest.Status == RefTestStatus.InProgress)
            return refTest.ToDto();

        refTest.Start();
        await context.SaveChangesAsync(cancellationToken);
        
        // Publish subscription event
        await subscriptionService.PublishRefTestStartedAsync(
            refTest.Id,
            refTest.Status,
            refTest.StartedAt!.Value,
            cancellationToken);

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
    /// <param name="subscriptionService"></param>
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
        [Service] IRefTestSubscriptionService subscriptionService,
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

        await context.SaveChangesAsync(cancellationToken);
        
        // Publish subscription event
        await subscriptionService.PublishRefTestCompletedAsync(
            refTest.Id,
            refTest.Status,
            refTest.CompletedAt!.Value,
            cancellationToken);

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
}

