using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

/// <summary>
/// Single-RefTest updates. Each use case loads the RefTest, applies the domain change, stages any
/// email the change makes due and saves once, so the email can never be lost or sent for a change
/// that was not persisted. A missing RefTest throws <see cref="RefTestNotFoundException"/>.
/// </summary>
public static class RefTestUpdateHandler
{
    /// <summary>
    /// Updates name and email. A changed email clears pending data-export requests for the old
    /// address and, when asked and an invitation was already sent, re-invites with a new token.
    /// </summary>
    public static async Task<RefTest> UpdateDetailsAsync(
        Guid id, string firstName, string lastName, string email, bool resendInvitation,
        IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var refTest = await LoadAsync(id, unitOfWork, cancellationToken);
        var previousEmail = refTest.Email;
        var emailChanged = previousEmail != email;
        var invitationWasSent = refTest.InvitationSentAt.HasValue;

        refTest.UpdateBasicDetails(firstName, lastName, email);

        if (emailChanged)
            await unitOfWork.ClearPersonalDataExportRequestsAsync(previousEmail, cancellationToken);

        if (emailChanged && resendInvitation && invitationWasSent)
        {
            refTest.RegenerateToken();
            await unitOfWork.StageInvitationEmailAsync(refTest, executeAfter: null, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return refTest;
    }

    /// <summary>
    /// Updates title, question count, time limit and questions. Specific question numbers win over
    /// random questions; with neither, the current questions are kept.
    /// </summary>
    public static async Task<RefTest> UpdateConfigurationAsync(
        Guid id, Guid titleId, int numberOfQuestions, int maxTimeInMinutes,
        IReadOnlyList<string>? specificQuestionNumbers, bool randomQuestions, IIhfRulesQuestionsService questionsService,
        IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var refTest = await LoadAsync(id, unitOfWork, cancellationToken);

        IReadOnlyList<string> questionIds;
        if (specificQuestionNumbers is not null)
            questionIds = await questionsService.GetQuestionIdsByNumberAsync([.. specificQuestionNumbers], cancellationToken);
        else if (randomQuestions)
            questionIds = await questionsService.GetRandomQuestionIdsAsync(numberOfQuestions, cancellationToken);
        else
            questionIds = refTest.QuestionIds;

        refTest.UpdateTestConfiguration(titleId, numberOfQuestions, maxTimeInMinutes, questionIds);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return refTest;
    }

    /// <summary>Gives an in-progress participant more time and tells connected clients.</summary>
    public static async Task<RefTest> ExtendTimeAsync(
        Guid id, int additionalMinutes, IRefTestSubscriptionService subscriptionService, TimeProvider timeProvider,
        IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var refTest = await LoadAsync(id, unitOfWork, cancellationToken);

        refTest.ExtendTime(additionalMinutes);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await subscriptionService.PublishTimeExtendedAsync(
            refTest.Id, refTest.MaxTimeInMinutes, additionalMinutes, timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return refTest;
    }

    /// <summary>
    /// Updates the automatic-email settings. Switching one on sends what is now due: the invitation
    /// for a pending RefTest that never got one, or the results for a completed RefTest that never
    /// got them.
    /// </summary>
    public static async Task<RefTest> UpdateNotificationSettingsAsync(
        Guid id, bool? sendInvitationsAutomatically, bool? sendResultsAutomatically,
        IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var refTest = await LoadAsync(id, unitOfWork, cancellationToken);
        var invitationsWereAutomatic = refTest.SendInvitationsAutomatically;
        var resultsWereAutomatic = refTest.SendResultsAutomatically;
        var invitationWasSent = refTest.InvitationSentAt.HasValue;
        var resultWasSent = refTest.ResultsSentAt.HasValue;

        refTest.UpdateNotificationSettings(sendInvitationsAutomatically, sendResultsAutomatically);

        if (sendInvitationsAutomatically == true && !invitationsWereAutomatic
            && refTest.Status == RefTestStatus.Pending && !invitationWasSent)
        {
            refTest.RegenerateToken();
            await unitOfWork.StageInvitationEmailAsync(refTest, executeAfter: null, cancellationToken);
        }

        if (sendResultsAutomatically == true && !resultsWereAutomatic
            && refTest.Status == RefTestStatus.Completed && !resultWasSent)
        {
            await unitOfWork.StageResultEmailAsync(
                new ResultEmailPayload(
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
                    [.. refTest.WrongAnswerIds]),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return refTest;
    }

    /// <summary>
    /// Issues a new token. A participant who already had an invitation gets a new one in the same
    /// save: a rotated token that never reaches them locks them out of the test.
    /// </summary>
    public static async Task<RefTest> RegenerateTokenAsync(
        Guid id, IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var refTest = await LoadAsync(id, unitOfWork, cancellationToken);
        var invitationWasSent = refTest.InvitationSentAt.HasValue;

        refTest.RegenerateToken();
        if (invitationWasSent)
            await unitOfWork.StageInvitationEmailAsync(refTest, executeAfter: null, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return refTest;
    }

    private static async Task<RefTest> LoadAsync(Guid id, IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        (await unitOfWork.GetRefTestsAsync([id], cancellationToken)).SingleOrDefault()
        ?? throw new RefTestNotFoundException(id);
}
