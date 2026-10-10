using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;

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
    /// <param name="sessionTokenService"></param>
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
    public static async Task<ParticipantRefTestDto> StartRefTestAsync(
        string token,
        RefTestManagementContext context,
        [Service] RefTestExpirationConfiguration configuration,
        [Service] PrivacyConfiguration privacyConfiguration,
        [Service] IRefTestSessionTokenService sessionTokenService,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        token = ParticipantInput.Token(token);

        var refTest = await context.RefTests
            .FindByParticipantCredentialAsync(token, sessionTokenService, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException();

        if (refTest.Status == RefTestStatus.InProgress)
            return refTest.ToParticipantDto();

        if (refTest.IsExpired(configuration.ExpirationIfNotStarted, timeProvider?.GetUtcNow().UtcDateTime))
        {
            refTest.Expire(timeProvider?.GetUtcNow().UtcDateTime);
            await context.SaveChangesWithRetryAsync(cancellationToken);
            
            // Publish subscription event
            await subscriptionService.PublishRefTestExpiredAsync(
                refTest.Id,
                refTest.Status,
                DateTime.UtcNow,
                cancellationToken);
            
            throw new RefTestExpiredException();
        }

        refTest.Start(privacyConfiguration.NoticeVersion, timeProvider?.GetUtcNow().UtcDateTime);
        await context.SaveChangesWithRetryAsync(cancellationToken);
        
        // Publish subscription event
        await subscriptionService.PublishRefTestStartedAsync(
            refTest.Id,
            refTest.Status,
            refTest.StartedAt!.Value,
            cancellationToken);

        return refTest.ToParticipantDto();
    }

    /// <summary>
    /// Exchanges an accepted participant invitation or session credential for a fresh,
    /// time-limited session credential.
    /// </summary>
    /// <param name="token">The participant invitation or session credential.</param>
    /// <param name="context">The database context used to resolve the participant.</param>
    /// <param name="sessionTokenService">Creates the protected session credential.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The new participant session credential.</returns>
    [Error<RefTestNotFoundException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantSessionDto> CreateRefTestSessionAsync(
        string token,
        RefTestManagementContext context,
        [Service] IRefTestSessionTokenService sessionTokenService,
        CancellationToken cancellationToken)
    {
        token = ParticipantInput.Token(token);

        var refTest = await context.RefTests
            .FindByParticipantCredentialAsync(token, sessionTokenService, cancellationToken);

        if (refTest is null || refTest.IsAnonymized)
            throw new RefTestNotFoundException();

        if (!refTest.PrivacyNoticeAcceptedAt.HasValue)
            throw new RefTestValidationException(
                "The privacy notice must be accepted before creating a participant session.");

        return new ParticipantSessionDto(sessionTokenService.Create(refTest));
    }

    /// <summary>
    /// Records explicit acceptance of the currently published privacy notice.
    /// </summary>
    [Error<RefTestNotFoundException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantRefTestDto> AcceptPrivacyNoticeAsync(
        string token,
        string noticeVersion,
        [Service] PrivacyConfiguration privacyConfiguration,
        [Service] IPrivacyNoticeAcceptanceUnitOfWork unitOfWork,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        if (noticeVersion != privacyConfiguration.NoticeVersion)
            throw new RefTestValidationException("The privacy notice has changed. Please review the current version.");

        token = ParticipantInput.Token(token);

        var refTest = await new AcceptPrivacyNoticeHandler(
                unitOfWork,
                timeProvider ?? TimeProvider.System)
            .HandleAsync(token, noticeVersion, cancellationToken);

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

    /// <summary>
    /// Save RefTest progress (current question and selected answers)
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="sessionTokenService">Validates participant session credentials.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantRefTestDto> SaveRefTestProgressAsync(
        SaveRefTestProgressInput input,
        RefTestManagementContext context,
        [Service] IRefTestSessionTokenService sessionTokenService,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var token = ParticipantInput.Token(input.Token);
        var selectedAnswerIds = ParticipantInput.AnswerIds(input.SelectedAnswerIds);
        var language = ParticipantInput.Language(input.Language);

        var refTest = await context.RefTests
            .FindByParticipantCredentialAsync(token, sessionTokenService, cancellationToken);

        if (refTest == null)
            throw new RefTestNotFoundException();

        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        var currentQuestionIndex = ParticipantInput.QuestionIndex(input.CurrentQuestionIndex, refTest);

        refTest.SaveProgress(currentQuestionIndex, selectedAnswerIds, language, timeProvider?.GetUtcNow().UtcDateTime);
        await context.SaveChangesWithRetryAsync(cancellationToken);

        return refTest.ToParticipantDto();
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
    /// <param name="sessionTokenService">Validates participant session credentials.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestValidationException>]
    public static async Task<ParticipantRefTestDto> CompleteRefTestAsync(
        CompleteRefTestInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] EmailConfiguration emailConfiguration,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IRefTestSessionTokenService sessionTokenService,
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        var token = ParticipantInput.Token(input.Token);
        var selectedAnswerIds = ParticipantInput.AnswerIds(input.SelectedAnswerIds);
        var language = ParticipantInput.Language(input.Language);

        var refTest = await context.RefTests
            .FindByParticipantCredentialAsync(token, sessionTokenService, cancellationToken)
            ?? throw new RefTestNotFoundException();

        // The completion source is fixed here: whether the time limit is enforced is not something
        // a caller of the API may choose.
        await new CompleteRefTestHandler(
                ihfRulesQuestionsService, subscriptionService, emailConfiguration,
                timeProvider ?? TimeProvider.System)
            .HandleAsync(refTest, selectedAnswerIds, language, RefTestCompletionSource.Participant,
                new EfRefTestUnitOfWork(context, jobEnqueueService), cancellationToken);
        return refTest.ToParticipantDto();
    }
}