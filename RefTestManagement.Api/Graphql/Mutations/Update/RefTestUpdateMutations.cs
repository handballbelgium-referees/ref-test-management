using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

/// <summary>
/// RefTest update mutations
/// </summary>
[MutationType]
public static partial class RefTestUpdateMutations
{
    /// <summary>
    /// Update RefTest participant details (firstName, lastName, email)
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.UpdateDetails)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> UpdateRefTestDetailsAsync(
        UpdateRefTestDetailsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == input.Id, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(input.Id);

        var emailChanged = refTest.Email != input.Email;
        var invitationWasSent = refTest.InvitationSentAt.HasValue;

        refTest.UpdateBasicDetails(input.FirstName, input.LastName, input.Email);

        // If the email was changed and the ResendInvitation flag is true and the invitation was
        // previously sent, resend it — staged into the same save as the address change so the
        // invitation can never be sent to an address that was not persisted, or dropped after it was.
        if (emailChanged && input.ResendInvitation && invitationWasSent)
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
                cancellationToken: cancellationToken, saveChanges: false,
                unitOfWorkContext: context);
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Update RefTest configuration (titleId, numberOfQuestions, maxTimeInMinutes, questionIds)
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.UpdateConfiguration)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> UpdateRefTestConfigurationAsync(
        UpdateRefTestConfigurationInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .Include(rt => rt.Title)
            .FirstOrDefaultAsync(rt => rt.Id == input.Id, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(input.Id);

        // Get questionIds based on input parameters (similar to CreateBulkRefTestsAsync)
        List<string> questionIds;

        if (input.SpecificQuestionNumbers is not null)
        {
            // Convert question numbers to question IDs
            questionIds = await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(
                input.SpecificQuestionNumbers,
                cancellationToken);
        }
        else if (input.RandomQuestions)
        {
            // Generate random question IDs
            questionIds = await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(
                input.NumberOfQuestions,
                cancellationToken);
        }
        else
        {
            // Keep existing question IDs if neither specific nor random is specified
            questionIds = refTest.QuestionIds;
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
                await context.SaveChangesWithRetryAsync(cancellationToken);

                titleId = title.Id;
                break;
            }
            default:
                throw new ArgumentException("Either Title.Id or Title.Name must be provided.");
        }

        refTest.UpdateTestConfiguration(
            titleId,
            input.NumberOfQuestions,
            input.MaxTimeInMinutes,
            questionIds);

        await context.SaveChangesWithRetryAsync(cancellationToken);

        await context.Entry(refTest).Reference(r => r.Title).LoadAsync(cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Extend time for an in-progress RefTest
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="subscriptionService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.ExtendTime)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> ExtendRefTestTimeAsync(
        ExtendRefTestTimeInput input,
        RefTestManagementContext context,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == input.Id, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(input.Id);

        refTest.ExtendTime(input.AdditionalMinutes);
        await context.SaveChangesWithRetryAsync(cancellationToken);

        // Publish subscription event for real-time UI updates
        await subscriptionService.PublishTimeExtendedAsync(
            refTest.Id,
            refTest.MaxTimeInMinutes,
            input.AdditionalMinutes,
            DateTime.UtcNow,
            cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Update RefTest notification settings
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    [Authorize(Policy = Permissions.RefTests.UpdateNotifications)]
    [Error<RefTestNotFoundException>]
    public static async Task<RefTestDto> UpdateRefTestNotificationSettingsAsync(
        UpdateRefTestNotificationSettingsInput input,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == input.Id, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(input.Id);

        // Track the current state before update
        var wasInvitationAutoSendEnabled = refTest.SendInvitationsAutomatically;
        var wasResultAutoSendEnabled = refTest.SendResultsAutomatically;
        var invitationWasSent = refTest.InvitationSentAt.HasValue;
        var resultWasSent = refTest.ResultsSentAt.HasValue;

        refTest.UpdateNotificationSettings(
            input.SendInvitationsAutomatically,
            input.SendResultsAutomatically);

        // Any email this settings change makes due is staged alongside it and committed by the
        // single save below.

        // If SendInvitationsAutomatically was just enabled (changed from false to true)
        // and the test is Pending and the invitation was never sent, send it now
        if (input.SendInvitationsAutomatically.HasValue &&
            !wasInvitationAutoSendEnabled &&
            input.SendInvitationsAutomatically.Value &&
            refTest.Status == RefTestStatus.Pending &&
            !invitationWasSent)
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
                cancellationToken: cancellationToken, saveChanges: false,
                unitOfWorkContext: context);
        }

        // If SendResultsAutomatically was just enabled (changed from false to true)
        // and the test is Completed and results were never sent, send them now
        if (input.SendResultsAutomatically.HasValue &&
            !wasResultAutoSendEnabled &&
            input.SendResultsAutomatically.Value &&
            refTest.Status == RefTestStatus.Completed &&
            !resultWasSent)
        {
            var resultPayload = new ResultEmailPayload(
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

            await jobEnqueueService.EnqueueResultEmailAsync(resultPayload,
                cancellationToken: cancellationToken, saveChanges: false,
                unitOfWorkContext: context);
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        return refTest.ToDto();
    }

    /// <summary>
    /// Regenerate the access token for a pending or expired RefTest
    /// </summary>
    /// <param name="refTestId"></param>
    /// <param name="context"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.RegenerateToken)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> RegenerateRefTestTokenAsync(
        [ID<RefTestDto>] Guid refTestId,
        RefTestManagementContext context,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == refTestId, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException(refTestId);

        // Check if the invitation was previously sent
        var invitationWasSent = refTest.InvitationSentAt.HasValue;

        refTest.RegenerateToken();

        // If an invitation was previously sent, send a new one with the new token. Staged into the
        // same save: a rotated token that never reaches the participant locks them out of the test.
        if (invitationWasSent)
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
                cancellationToken: cancellationToken, saveChanges: false,
                unitOfWorkContext: context);
        }

        await context.SaveChangesWithRetryAsync(cancellationToken);

        return refTest.ToDto();
    }
}
