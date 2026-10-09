using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Update;

public sealed class RefTestUpdateHandler(
    IRefTestRepository refTestRepository,
    IRefTestTitleRepository titleRepository,
    IPersonalDataExportRequestRepository exportRequestRepository,
    IUnitOfWork unitOfWork,
    IIhfRulesQuestionsService ihfRulesQuestionsService,
    IJobEnqueueService jobEnqueueService,
    IRefTestSubscriptionService subscriptionService)
{
    public async Task<RefTest> UpdateDetailsAsync(
        UpdateRefTestDetailsCommand command, CancellationToken cancellationToken = default)
    {
        var refTest = await FindRequiredAsync(command.Id, cancellationToken);
        var emailChanged = refTest.Email != command.Email;
        var previousEmail = refTest.Email;
        var invitationWasSent = refTest.InvitationSentAt.HasValue;
        refTest.UpdateBasicDetails(command.FirstName, command.LastName, command.Email);
        if (emailChanged)
            await exportRequestRepository.ClearForEmailAsync(previousEmail, cancellationToken);

        // If the email was changed and the ResendInvitation flag is true and the invitation was
        // previously sent, resend it — staged into the same save as the address change so the
        // invitation can never be sent to an address that was not persisted, or dropped after it.
        if (emailChanged && command.ResendInvitation && invitationWasSent)
        {
            refTest.RegenerateToken();
            await jobEnqueueService.EnqueueInvitationEmailAsync(
                refTest, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
        }

        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        return refTest;
    }

    public async Task<RefTest> UpdateConfigurationAsync(
        UpdateRefTestConfigurationCommand command, CancellationToken cancellationToken = default)
    {
        var refTest = await FindRequiredAsync(command.Id, cancellationToken);
        List<string> questionIds;
        if (command.SpecificQuestionNumbers is not null)
        {
            questionIds = await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(
                command.SpecificQuestionNumbers, cancellationToken);
        }
        else if (command.RandomQuestions)
        {
            questionIds = await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(
                command.NumberOfQuestions, cancellationToken);
        }
        else
        {
            questionIds = refTest.QuestionIds;
        }

        Guid titleId;
        switch (command.TitleId)
        {
            case not null:
                titleId = command.TitleId.Value;
                break;
            case null when command.TitleName is not null:
            {
                var title = await titleRepository.ResolveOrCreateAsync(command.TitleName, cancellationToken);
                titleId = title.Id;
                break;
            }
            default:
                throw new ArgumentException("Either Title.Id or Title.Name must be provided.");
        }

        refTest.UpdateTestConfiguration(titleId, command.NumberOfQuestions, command.MaxTimeInMinutes, questionIds);
        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        await refTestRepository.LoadTitleAsync(refTest, cancellationToken);
        return refTest;
    }

    public async Task<RefTest> ExtendTimeAsync(
        ExtendRefTestTimeCommand command, CancellationToken cancellationToken = default)
    {
        var refTest = await FindRequiredAsync(command.Id, cancellationToken);
        refTest.ExtendTime(command.AdditionalMinutes);
        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        // Publish subscription event for real-time UI updates
        await subscriptionService.PublishTimeExtendedAsync(
            refTest.Id, refTest.MaxTimeInMinutes, command.AdditionalMinutes, DateTime.UtcNow, cancellationToken);
        return refTest;
    }

    public async Task<RefTest> UpdateNotificationSettingsAsync(
        UpdateRefTestNotificationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var refTest = await FindRequiredAsync(command.Id, cancellationToken);
        // Track the current state before update
        var wasInvitationAutoSendEnabled = refTest.SendInvitationsAutomatically;
        var wasResultAutoSendEnabled = refTest.SendResultsAutomatically;
        var invitationWasSent = refTest.InvitationSentAt.HasValue;
        var resultWasSent = refTest.ResultsSentAt.HasValue;
        refTest.UpdateNotificationSettings(command.SendInvitationsAutomatically, command.SendResultsAutomatically);

        // Any email this settings change makes due is staged alongside it and committed by the
        // single save below.
        // If SendInvitationsAutomatically was just enabled (changed from false to true)
        // and the test is Pending and the invitation was never sent, send it now
        if (command.SendInvitationsAutomatically.HasValue
            && !wasInvitationAutoSendEnabled
            && command.SendInvitationsAutomatically.Value
            && refTest.Status == RefTestStatus.Pending
            && !invitationWasSent)
        {
            refTest.RegenerateToken();
            await jobEnqueueService.EnqueueInvitationEmailAsync(
                refTest, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
        }

        // If SendResultsAutomatically was just enabled (changed from false to true)
        // and the test is Completed and results were never sent, send them now
        if (command.SendResultsAutomatically.HasValue
            && !wasResultAutoSendEnabled
            && command.SendResultsAutomatically.Value
            && refTest.Status == RefTestStatus.Completed
            && !resultWasSent)
        {
            var resultPayload = new ResultEmailPayload(
                refTest.Id, refTest.FullName, refTest.Email,
                refTest.QuestionScore ?? 0, refTest.AnswerScore ?? 0,
                refTest.QuestionTotal, refTest.AnswerTotal ?? 0, refTest.Percentage ?? 0,
                refTest.SelectedAnswerIds, refTest.WrongQuestionIds, refTest.WrongAnswerIds);
            await jobEnqueueService.EnqueueResultEmailAsync(
                resultPayload, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
        }

        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        return refTest;
    }

    public async Task<RefTest> RegenerateTokenAsync(
        RegenerateRefTestTokenCommand command, CancellationToken cancellationToken = default)
    {
        var refTest = await FindRequiredAsync(command.Id, cancellationToken);
        // Check if the invitation was previously sent
        var invitationWasSent = refTest.InvitationSentAt.HasValue;
        refTest.RegenerateToken();
        // If an invitation was previously sent, send a new one with the new token. Staged into the
        // same save: a rotated token that never reaches the participant locks them out of the test.
        if (invitationWasSent)
        {
            await jobEnqueueService.EnqueueInvitationEmailAsync(
                refTest, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
        }
        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        return refTest;
    }

    private async Task<RefTest> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await refTestRepository.FindByIdAsync(id, cancellationToken) ?? throw new RefTestNotFoundException(id);
}
