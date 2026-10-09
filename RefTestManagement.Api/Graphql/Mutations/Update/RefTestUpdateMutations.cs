using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests.Update;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
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
    /// <param name="input">Participant details to update.</param>
    /// <param name="handler">Application handler for RefTest updates.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.UpdateDetails)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> UpdateRefTestDetailsAsync(
        UpdateRefTestDetailsInput input,
        [Service] RefTestUpdateHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.UpdateDetailsAsync(
            new UpdateRefTestDetailsCommand(input.Id, input.FirstName, input.LastName, input.Email, input.ResendInvitation),
            cancellationToken);
        return refTest.ToDto();
    }

    /// <summary>
    /// Update RefTest configuration (titleId, numberOfQuestions, maxTimeInMinutes, questionIds)
    /// </summary>
    /// <param name="input">Configuration values to update.</param>
    /// <param name="handler">Application handler for RefTest updates.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.UpdateConfiguration)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> UpdateRefTestConfigurationAsync(
        UpdateRefTestConfigurationInput input,
        [Service] RefTestUpdateHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.UpdateConfigurationAsync(
            new UpdateRefTestConfigurationCommand(
                input.Id, input.Title.Id, input.Title.Name, input.NumberOfQuestions, input.MaxTimeInMinutes,
                input.SpecificQuestionNumbers, input.RandomQuestions),
            cancellationToken);
        return refTest.ToDto();
    }

    /// <summary>
    /// Extend time for an in-progress RefTest
    /// </summary>
    /// <param name="input">Time extension values.</param>
    /// <param name="handler">Application handler for RefTest updates.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.ExtendTime)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> ExtendRefTestTimeAsync(
        ExtendRefTestTimeInput input,
        [Service] RefTestUpdateHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.ExtendTimeAsync(
            new ExtendRefTestTimeCommand(input.Id, input.AdditionalMinutes), cancellationToken);
        return refTest.ToDto();
    }

    /// <summary>
    /// Update RefTest notification settings
    /// </summary>
    /// <param name="input">Notification settings to update.</param>
    /// <param name="handler">Application handler for RefTest updates.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    [Authorize(Policy = Permissions.RefTests.UpdateNotifications)]
    [Error<RefTestNotFoundException>]
    public static async Task<RefTestDto> UpdateRefTestNotificationSettingsAsync(
        UpdateRefTestNotificationSettingsInput input,
        [Service] RefTestUpdateHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.UpdateNotificationSettingsAsync(
            new UpdateRefTestNotificationSettingsCommand(
                input.Id, input.SendInvitationsAutomatically, input.SendResultsAutomatically),
            cancellationToken);
        return refTest.ToDto();
    }

    /// <summary>
    /// Regenerate the access token for a pending or expired RefTest
    /// </summary>
    /// <param name="refTestId">The ID of the RefTest.</param>
    /// <param name="handler">Application handler for RefTest updates.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.RegenerateToken)]
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    public static async Task<RefTestDto> RegenerateRefTestTokenAsync(
        [ID<RefTestDto>] Guid refTestId,
        [Service] RefTestUpdateHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.RegenerateTokenAsync(new RegenerateRefTestTokenCommand(refTestId), cancellationToken);
        return refTest.ToDto();
    }
}
