using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;

/// <summary>
/// RefTest lifecycle mutations (start, save progress, complete)
/// </summary>
[MutationType]
public static partial class RefTestLifecycleMutations
{
    /// <summary>Start a RefTest.</summary>
    /// <param name="token">The participant invitation or session credential.</param>
    /// <param name="handler">The Application lifecycle handler.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The participant RefTest.</returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="RefTestExpiredException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<RefTestExpiredException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantRefTestDto> StartRefTestAsync(
        string token,
        [Service] RefTestLifecycleHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.StartAsync(token, cancellationToken);
        return refTest.ToParticipantDto();
    }

    /// <summary>
    /// Exchanges an accepted participant invitation or session credential for a fresh,
    /// time-limited session credential.
    /// </summary>
    /// <param name="token">The participant invitation or session credential.</param>
    /// <param name="handler">The Application lifecycle handler.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The new participant session credential.</returns>
    [Error<RefTestNotFoundException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantSessionDto> CreateRefTestSessionAsync(
        string token,
        [Service] RefTestLifecycleHandler handler,
        CancellationToken cancellationToken)
    {
        var sessionToken = await handler.CreateSessionAsync(token, cancellationToken);
        return new ParticipantSessionDto(sessionToken);
    }

    /// <summary>Records explicit acceptance of the currently published privacy notice.</summary>
    [Error<RefTestNotFoundException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantRefTestDto> AcceptPrivacyNoticeAsync(
        string token,
        string noticeVersion,
        [Service] RefTestLifecycleHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.AcceptPrivacyNoticeAsync(token, noticeVersion, cancellationToken);
        return refTest.ToParticipantDto();
    }

    /// <summary>
    /// Queues a durable one-RefTest withdrawal for the holder of its invitation or session token. The
    /// background worker performs the anonymization and publishes the update after it commits.
    /// </summary>
    [Error<RefTestNotFoundException>]
    [Error<RefTestValidationException>]
    public static async Task<bool> WithdrawConsentAsync(
        string token,
        [Service] IPrivacyWithdrawalRequestService withdrawalRequestService,
        CancellationToken cancellationToken)
    {
        token = ParticipantInput.Token(token);

        if (!await withdrawalRequestService.RequestForParticipantAsync(token, cancellationToken))
            throw new RefTestNotFoundException();

        return true;
    }

    /// <summary>Save RefTest progress (current question and selected answers).</summary>
    /// <param name="input">The participant's progress submission.</param>
    /// <param name="handler">The Application lifecycle handler.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The updated participant RefTest.</returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantRefTestDto> SaveRefTestProgressAsync(
        SaveRefTestProgressInput input,
        [Service] RefTestLifecycleHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.SaveProgressAsync(
            new SaveRefTestProgressCommand(
                input.Token, input.CurrentQuestionIndex, input.SelectedAnswerIds, input.Language),
            cancellationToken);
        return refTest.ToParticipantDto();
    }

    /// <summary>Complete a RefTest and (optional) send results email.</summary>
    /// <param name="input">The participant's completion submission.</param>
    /// <param name="handler">The Application lifecycle handler.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The completed participant RefTest.</returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantRefTestDto> CompleteRefTestAsync(
        CompleteRefTestInput input,
        [Service] RefTestLifecycleHandler handler,
        CancellationToken cancellationToken)
    {
        var refTest = await handler.CompleteAsync(
            new CompleteRefTestCommand(input.Token, input.SelectedAnswerIds, input.Language),
            cancellationToken);
        return refTest.ToParticipantDto();
    }
}
