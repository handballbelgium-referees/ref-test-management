using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

/// <summary>Builds the result email payload from a completed RefTest's current scores.</summary>
public static class ResultEmailPayloadFactory
{
    public static ResultEmailPayload For(RefTest refTest) =>
        new(
            refTest.Id,
            refTest.FullName,
            refTest.Email,
            refTest.QuestionScore ?? 0,
            refTest.AnswerScore ?? 0,
            refTest.QuestionTotal,
            refTest.AnswerTotal ?? 0,
            refTest.Percentage ?? 0,
            [.. refTest.SelectedAnswerIds],
            [.. refTest.WrongQuestionIds],
            [.. refTest.WrongAnswerIds]);
}

/// <summary>
/// Completes an in-progress RefTest: scores the answers through the question bank, records the
/// result, and when results go out automatically stages the result email in the same save, so a
/// completed RefTest can never lose the email it owes the participant.
/// </summary>
public sealed class CompleteRefTestHandler(
    IIhfRulesQuestionsService questionsService,
    IRefTestSubscriptionService subscriptionService,
    EmailConfiguration emailConfiguration,
    TimeProvider timeProvider)
{
    /// <param name="source">
    /// Who completes the test. Only a participant is held to the time limit; the expiration service
    /// completes overdue tests on the participant's behalf.
    /// </param>
    public async Task<RefTest> HandleAsync(
        RefTest refTest,
        IReadOnlyList<string> selectedAnswerIds,
        string? language,
        RefTestCompletionSource source,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        var score = await questionsService.CalculateScoreAsync(refTest.QuestionIds, selectedAnswerIds, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        refTest.Complete(
            score.QuestionScore, score.AnswerScore, score.AnswerTotal, score.Percentage,
            selectedAnswerIds, score.WrongQuestionIds, score.WrongAnswerIds,
            language, source, now);

        if (refTest.SendResultsAutomatically)
        {
            DateTime? executeAfter = emailConfiguration.ScheduledDelayMinutes > 0
                ? now.AddMinutes(emailConfiguration.ScheduledDelayMinutes)
                : null;
            await unitOfWork.StageResultEmailAsync(ResultEmailPayloadFactory.For(refTest), executeAfter, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
            refTest.SelectedAnswerIds,
            cancellationToken);

        return refTest;
    }
}
