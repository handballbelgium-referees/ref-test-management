using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

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
        var refTest = await RefTestUpdateHandler.UpdateDetailsAsync(
            input.Id, input.FirstName, input.LastName, input.Email, input.ResendInvitation,
            new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);
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
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        // Title resolution is shared with creation and may stage a new title in this context, so it
        // runs here, before the use case saves.
        var titleId = input.Title.Id
                      ?? (input.Title.Name is not null
                          ? (await RefTestTitleResolution.ResolveOrCreateAsync(context, input.Title.Name, cancellationToken)).Id
                          : throw new ArgumentException("Either Title.Id or Title.Name must be provided."));

        var refTest = await RefTestUpdateHandler.UpdateConfigurationAsync(
            input.Id, titleId, input.NumberOfQuestions, input.MaxTimeInMinutes,
            input.SpecificQuestionNumbers, input.RandomQuestions, ihfRulesQuestionsService,
            new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);

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
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var refTest = await RefTestUpdateHandler.ExtendTimeAsync(
            input.Id, input.AdditionalMinutes, subscriptionService, timeProvider ?? TimeProvider.System,
            new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);
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
        var refTest = await RefTestUpdateHandler.UpdateNotificationSettingsAsync(
            input.Id, input.SendInvitationsAutomatically, input.SendResultsAutomatically,
            new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);
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
        var refTest = await RefTestUpdateHandler.RegenerateTokenAsync(
            refTestId, new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);
        return refTest.ToDto();
    }
}
