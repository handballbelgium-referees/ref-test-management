using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Creation;

public sealed class RefTestCreationHandler(
    IRefTestRepository refTestRepository,
    IRefTestTitleRepository titleRepository,
    IUnitOfWork unitOfWork,
    IIhfRulesQuestionsService ihfRulesQuestionsService,
    IJobEnqueueService jobEnqueueService,
    IRefTestSubscriptionService subscriptionService,
    ICurrentUser currentUser,
    ILogger<RefTestCreationHandler> logger)
{
    public async Task<CreateRefTestsResult> HandleAsync(
        CreateRefTestsCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = new CreateRefTestsResult { TotalRequested = command.Users.Count };
        var (titleId, titleValue) = await ResolveTitleAsync(command, cancellationToken);
        var sharedQuestionIds = await ResolveSharedQuestionIdsAsync(command, cancellationToken);
        var createdRefTests = await BuildRefTestsAsync(command, titleId, sharedQuestionIds, result, cancellationToken);

        if (createdRefTests.Count == 0)
            return result;

        if (command.RequiresApproval)
        {
            var checkpoint = unitOfWork.CaptureStagedJobIds();
            try
            {
                var payload = new ApprovalNotificationEmailPayload(
                    currentUser.DisplayName, currentUser.Email, titleValue,
                    createdRefTests.Select(refTest => new ApprovalNotificationRefTestItem(
                        refTest.Id, refTest.FirstName, refTest.LastName, refTest.Email, refTest.ScheduledAt)).ToList());
                await jobEnqueueService.EnqueueApprovalNotificationAsync(
                    payload, saveChanges: false, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                unitOfWork.DiscardJobsStagedSince(checkpoint);
                result.Failed += createdRefTests.Count;
                result.Errors.AddRange(createdRefTests.Select(refTest => new CreateRefTestsError(
                    new CreateRefTestUser(refTest.FirstName, refTest.LastName, refTest.Email),
                    $"Approval notification could not be prepared: {MutationFailureHandling.GetUserSafeMessage(exception)}")));
                MutationFailureHandling.LogMutationFailure(logger, exception, "CreateRefTestsAsync", currentUser.CorrelationId);
                return result;
            }
        }
        else if (command.SendAutomatedInvitations)
            await EnqueueInvitationEmailsAsync(createdRefTests, result, cancellationToken);

        if (createdRefTests.Count == 0)
            return result;

        // RefTests and their outbox jobs commit together, preventing a persisted RefTest from
        // existing without its invitation or approval-notification job.
        refTestRepository.AddRange(createdRefTests);
        await unitOfWork.SaveChangesWithRetryAsync(cancellationToken);
        result.SuccessfullyCreated = createdRefTests.Count;

        foreach (var refTest in createdRefTests)
            await subscriptionService.PublishRefTestCreatedAsync(
                refTest.Id, refTest.FullName, refTest.Email,
                titleId, titleValue,
                refTest.InvitationSentAt.HasValue, refTest.ResultsSentAt.HasValue,
                refTest.SendInvitationsAutomatically, refTest.SendResultsAutomatically,
                refTest.Status, refTest.NumberOfQuestions, refTest.MaxTimeInMinutes,
                refTest.FirstName, refTest.LastName, refTest.CreatedAt, refTest.ScheduledAt,
                cancellationToken);

        if (command.RequiresApproval || !command.SendAutomatedInvitations)
            result.CreatedRefTests.AddRange(createdRefTests);

        return result;
    }

    private async Task<(Guid Id, string? Value)> ResolveTitleAsync(
        CreateRefTestsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TitleId is not null)
        {
            var existing = await titleRepository.FindByIdAsync(command.TitleId.Value, cancellationToken);
            return (command.TitleId.Value, existing?.Value);
        }

        if (command.TitleName is null)
            throw new ArgumentException("Either Title.Id or Title.Name must be provided.");

        var title = await titleRepository.ResolveOrCreateAsync(command.TitleName, cancellationToken);
        return (title.Id, title.Value);
    }

    private async Task<List<string>> ResolveSharedQuestionIdsAsync(
        CreateRefTestsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SpecificQuestionNumbers is not null)
            return await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(
                command.SpecificQuestionNumbers.ToList(), cancellationToken);

        if (!command.RandomQuestionsForEachUser)
            return await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(
                command.NumberOfQuestions, cancellationToken);

        return [];
    }

    private async Task<List<RefTest>> BuildRefTestsAsync(
        CreateRefTestsCommand command,
        Guid titleId,
        List<string> sharedQuestionIds,
        CreateRefTestsResult result,
        CancellationToken cancellationToken)
    {
        var created = new List<RefTest>();
        foreach (var user in command.Users)
        {
            try
            {
                var questionIds = command.RandomQuestionsForEachUser
                    ? await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(command.NumberOfQuestions, cancellationToken)
                    : sharedQuestionIds;

                created.Add(RefTest.Create(
                    titleId, user.FirstName, user.LastName, user.Email,
                    command.NumberOfQuestions, command.MaxTimeInMinutes, questionIds,
                    command.SendAutomatedInvitations, command.SendAutomatedResults,
                    requiresApproval: command.RequiresApproval,
                    scheduledAt: command.ScheduledAt,
                    creatorName: currentUser.DisplayName,
                    creatorEmail: currentUser.Email));
            }
            catch (Exception exception)
            {
                result.Failed++;
                result.Errors.Add(new CreateRefTestsError(user, MutationFailureHandling.GetUserSafeMessage(exception)));
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, "CreateRefTestsAsync", currentUser.CorrelationId);
            }
        }

        return created;
    }

    private async Task EnqueueInvitationEmailsAsync(
        List<RefTest> refTests,
        CreateRefTestsResult result,
        CancellationToken cancellationToken)
    {
        foreach (var refTest in refTests.ToList())
        {
            var checkpoint = unitOfWork.CaptureStagedJobIds();
            try
            {
                await jobEnqueueService.EnqueueInvitationEmailAsync(
                    refTest, executeAfter: refTest.ScheduledAt, saveChanges: false,
                    unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);
                result.CreatedRefTests.Add(refTest);
            }
            catch (Exception exception)
            {
                unitOfWork.DiscardJobsStagedSince(checkpoint);
                refTests.Remove(refTest);
                result.Failed++;
                result.Errors.Add(new CreateRefTestsError(
                    new CreateRefTestUser(refTest.FirstName, refTest.LastName, refTest.Email),
                    $"Invitation email could not be prepared: {MutationFailureHandling.GetUserSafeMessage(exception)}"));
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, "CreateRefTestsAsync", currentUser.CorrelationId, refTest.Id);
            }
        }
    }
}
