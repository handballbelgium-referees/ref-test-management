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
public static partial class RefTestLifecycleMutations
{
    /// <summary>
    /// Start a RefTest
    /// </summary>
    /// <param name="token"></param>
    /// <param name="context"></param>
    /// <param name="configuration"></param>
    /// <param name="privacyConfiguration"></param>
    /// <param name="subscriptionService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="RefTestExpiredException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<RefTestExpiredException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestValidationException>]
    public static async Task<RefTestDto> StartRefTestAsync(
        string token,
        RefTestManagementContext context,
        [Service] RefTestExpirationConfiguration configuration,
        [Service] PrivacyConfiguration privacyConfiguration,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        ParticipantInput.Token(token);

        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(token);

        if (refTest.Status == RefTestStatus.InProgress)
            return refTest.ToDto();

        if (refTest.IsExpired(configuration.ExpirationIfNotStarted))
        {
            refTest.Expire();
            await context.SaveChangesWithRetryAsync(cancellationToken);
            
            // Publish subscription event
            await subscriptionService.PublishRefTestExpiredAsync(
                refTest.Id,
                refTest.Status,
                DateTime.UtcNow,
                cancellationToken);
            
            throw new RefTestExpiredException(token);
        }

        refTest.Start(privacyConfiguration.NoticeVersion);
        await context.SaveChangesWithRetryAsync(cancellationToken);
        
        // Publish subscription event
        await subscriptionService.PublishRefTestStartedAsync(
            refTest.Id,
            refTest.Status,
            refTest.StartedAt!.Value,
            cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Records explicit acceptance of the currently published privacy notice.
    /// </summary>
    [Error<RefTestNotFoundException>]
    [Error<RefTestValidationException>]
    public static async Task<RefTestDto> AcceptPrivacyNoticeAsync(
        string token,
        string noticeVersion,
        RefTestManagementContext context,
        [Service] PrivacyConfiguration privacyConfiguration,
        CancellationToken cancellationToken)
    {
        if (noticeVersion != privacyConfiguration.NoticeVersion)
            throw new RefTestValidationException("The privacy notice has changed. Please review the current version.");

        ParticipantInput.Token(token);

        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(token);

        refTest.AcceptPrivacyNotice(noticeVersion);
        await context.SaveChangesWithRetryAsync(cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Lets the holder of a RefTest token withdraw consent, anonymizing their personal data in
    /// place. Available regardless of RefTest status, including Completed. The record and its
    /// audit trail are always kept for accountability — only personal data is redacted.
    /// </summary>
    [Error<RefTestNotFoundException>]
    [Error<RefTestValidationException>]
    public static async Task<bool> WithdrawConsentAsync(
        string token,
        RefTestManagementContext context,
        [Service] IRefTestPrivacyErasureService privacyErasureService,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        ParticipantInput.Token(token);

        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(token);

        var refTestId = refTest.Id;
        var status = refTest.Status;
        await privacyErasureService.EraseAsync(refTest, ErasureInitiator.Participant, cancellationToken);

        await subscriptionService.PublishRefTestAnonymizedAsync(
            refTestId, status, refTest.FullName, refTest.Email, cancellationToken);

        return true;
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
    [Error<RefTestValidationException>]
    public static async Task<RefTestDto> SaveRefTestProgressAsync(
        SaveRefTestProgressInput input,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var token = ParticipantInput.Token(input.Token);
        var selectedAnswerIds = ParticipantInput.AnswerIds(input.SelectedAnswerIds);
        var language = ParticipantInput.Language(input.Language);

        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(input.Token);

        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        var currentQuestionIndex = ParticipantInput.QuestionIndex(input.CurrentQuestionIndex, refTest);

        refTest.SaveProgress(currentQuestionIndex, selectedAnswerIds, language);
        await context.SaveChangesWithRetryAsync(cancellationToken);

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
    [Error<RefTestValidationException>]
    public static Task<RefTestDto> CompleteRefTestAsync(
        CompleteRefTestInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] EmailConfiguration emailConfiguration,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken) =>
        CompleteRefTestCoreAsync(
            input,
            context,
            ihfRulesQuestionsService,
            jobEnqueueService,
            emailConfiguration,
            subscriptionService,
            RefTestCompletionSource.Participant,
            cancellationToken);

    /// <summary>
    /// Shared body behind <see cref="CompleteRefTestAsync"/>. Kept internal so it stays out of the
    /// GraphQL schema: <paramref name="source"/> decides whether the participant's time limit is
    /// enforced, and that is not something a caller of the API may choose.
    /// </summary>
    internal static async Task<RefTestDto> CompleteRefTestCoreAsync(
        CompleteRefTestInput input,
        RefTestManagementContext context,
        IIhfRulesQuestionsService ihfRulesQuestionsService,
        IJobEnqueueService jobEnqueueService,
        EmailConfiguration emailConfiguration,
        IRefTestSubscriptionService subscriptionService,
        RefTestCompletionSource source,
        CancellationToken cancellationToken)
    {
        var token = ParticipantInput.Token(input.Token);
        var selectedAnswerIds = ParticipantInput.AnswerIds(input.SelectedAnswerIds);
        var language = ParticipantInput.Language(input.Language);

        var refTest = await context.RefTests
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(input.Token);

        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        // Use existing score calculation logic
        var scoreResult = await ihfRulesQuestionsService.CalculateScoreAsync(
            refTest.QuestionIds,
            selectedAnswerIds,
            cancellationToken
        );

        // Complete RefTest with calculated results
        refTest.Complete(
            scoreResult.QuestionScore,
            scoreResult.AnswerScore,
            scoreResult.AnswerTotal,
            scoreResult.Percentage,
            selectedAnswerIds,
            scoreResult.WrongQuestionIds,
            scoreResult.WrongAnswerIds,
            language,
            source
        );

        // The completion and the result email it owes are committed together: a completed test
        // whose result job was lost never reports back to the participant.
        if (refTest.SendResultsAutomatically)
        {
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
            await jobEnqueueService.EnqueueResultEmailAsync(payload, scheduledAt,
                saveChanges: false,
                unitOfWorkContext: context,
                cancellationToken: cancellationToken);
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        // Publish subscription event
        await subscriptionService.PublishRefTestCompletedAsync(
            refTest.Id,
            refTest.Status,
            refTest.CompletedAt!.Value,
            refTest.QuestionScore ?? 0,
            refTest.QuestionTotal,
            refTest.AnswerScore ?? 0,
            refTest.AnswerTotal ?? 0,
            refTest.Percentage ?? 0,
            language ?? "",
            cancellationToken);

        return refTest.ToDto();
    }
}
