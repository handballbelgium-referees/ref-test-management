using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;

/// <summary>Executes participant-facing RefTest lifecycle operations.</summary>
public sealed class RefTestLifecycleHandler(
    IRefTestRepository refTestRepository,
    IUnitOfWork unitOfWork,
    IRefTestSessionTokenService sessionTokenService,
    RefTestExpirationConfiguration expirationConfiguration,
    PrivacyConfiguration privacyConfiguration,
    IIhfRulesQuestionsService ihfRulesQuestionsService,
    IJobEnqueueService jobEnqueueService,
    EmailConfiguration emailConfiguration,
    IRefTestSubscriptionService subscriptionService)
{
    public async Task<RefTest> StartAsync(string inputToken, CancellationToken cancellationToken = default)
    {
        var token = ParticipantInput.Token(inputToken);
        var refTest = await FindAsync(token, cancellationToken);
        if (refTest is null)
            throw new RefTestNotFoundException();

        if (refTest.Status == RefTestStatus.InProgress)
            return refTest;

        if (refTest.IsExpired(expirationConfiguration.ExpirationIfNotStarted))
        {
            refTest.Expire();
            await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
            await subscriptionService.PublishRefTestExpiredAsync(
                refTest.Id, refTest.Status, DateTime.UtcNow, cancellationToken);
            throw new RefTestExpiredException();
        }

        refTest.Start(privacyConfiguration.NoticeVersion);
        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        await subscriptionService.PublishRefTestStartedAsync(
            refTest.Id, refTest.Status, refTest.StartedAt!.Value, cancellationToken);
        return refTest;
    }

    public async Task<string> CreateSessionAsync(string inputToken, CancellationToken cancellationToken = default)
    {
        var token = ParticipantInput.Token(inputToken);
        var refTest = await FindAsync(token, cancellationToken);
        if (refTest is null || refTest.IsAnonymized)
            throw new RefTestNotFoundException();
        if (!refTest.PrivacyNoticeAcceptedAt.HasValue)
            throw new RefTestValidationException(
                "The privacy notice must be accepted before creating a participant session.");

        return sessionTokenService.Create(refTest);
    }

    public async Task<RefTest> AcceptPrivacyNoticeAsync(
        string inputToken,
        string noticeVersion,
        CancellationToken cancellationToken = default)
    {
        if (noticeVersion != privacyConfiguration.NoticeVersion)
            throw new RefTestValidationException("The privacy notice has changed. Please review the current version.");

        var token = ParticipantInput.Token(inputToken);
        var refTest = await FindAsync(token, cancellationToken);
        if (refTest is null)
            throw new RefTestNotFoundException();

        refTest.AcceptPrivacyNotice(noticeVersion);
        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        return refTest;
    }

    public async Task<RefTest> SaveProgressAsync(
        SaveRefTestProgressCommand command,
        CancellationToken cancellationToken = default)
    {
        var token = ParticipantInput.Token(command.Token);
        var selectedAnswerIds = ParticipantInput.AnswerIds(command.SelectedAnswerIds);
        var language = ParticipantInput.Language(command.Language);
        var refTest = await FindAsync(token, cancellationToken);
        if (refTest is null)
            throw new RefTestNotFoundException();
        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        var currentQuestionIndex = ParticipantInput.QuestionIndex(command.CurrentQuestionIndex, refTest);
        refTest.SaveProgress(currentQuestionIndex, selectedAnswerIds, language);
        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        return refTest;
    }

    public async Task<RefTest> CompleteAsync(
        CompleteRefTestCommand command,
        CancellationToken cancellationToken = default)
    {
        var token = ParticipantInput.Token(command.Token);
        var selectedAnswerIds = ParticipantInput.AnswerIds(command.SelectedAnswerIds);
        var language = ParticipantInput.Language(command.Language);
        var refTest = await FindAsync(token, cancellationToken);
        if (refTest is null)
            throw new RefTestNotFoundException();

        return await CompleteLoadedAsync(
            refTest, selectedAnswerIds, language, command.Source, cancellationToken);
    }

    /// <summary>Completes an already loaded RefTest, used by participant and expiry workflows.</summary>
    public async Task<RefTest> CompleteLoadedAsync(
        RefTest refTest,
        List<string> selectedAnswerIds,
        string? language,
        RefTestCompletionSource source,
        CancellationToken cancellationToken = default)
    {
        if (refTest.Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.InProgress);

        var scoreResult = await ihfRulesQuestionsService.CalculateScoreAsync(
            refTest.QuestionIds, selectedAnswerIds, cancellationToken);
        refTest.Complete(
            scoreResult.QuestionScore,
            scoreResult.AnswerScore,
            scoreResult.AnswerTotal,
            scoreResult.Percentage,
            selectedAnswerIds,
            scoreResult.WrongQuestionIds,
            scoreResult.WrongAnswerIds,
            language,
            source);

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
                refTest.WrongAnswerIds);

            DateTime? scheduledAt = emailConfiguration.ScheduledDelayMinutes > 0
                ? DateTime.UtcNow.AddMinutes(emailConfiguration.ScheduledDelayMinutes)
                : null;
            await jobEnqueueService.EnqueueResultEmailAsync(
                payload,
                scheduledAt,
                saveChanges: false,
                unitOfWorkContext: unitOfWork,
                cancellationToken: cancellationToken);
        }

        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
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

    private Task<RefTest?> FindAsync(string token, CancellationToken cancellationToken) =>
        refTestRepository.FindByParticipantCredentialAsync(token, cancellationToken: cancellationToken);
}
