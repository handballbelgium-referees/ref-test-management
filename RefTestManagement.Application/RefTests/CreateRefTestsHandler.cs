using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

/// <summary>One participant to create a RefTest for.</summary>
public sealed record RefTestParticipant(string FirstName, string LastName, string Email);

/// <summary>Everything the creation use case needs; the caller resolves identity, approval and title.</summary>
public sealed record CreateRefTestsCommand(
    IReadOnlyList<RefTestParticipant> Participants,
    Guid TitleId,
    string? TitleValue,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    bool RandomQuestionsForEachUser,
    IReadOnlyList<string>? SpecificQuestionNumbers,
    bool SendAutomatedInvitations,
    bool SendAutomatedResults,
    DateTime? ScheduledAt,
    bool RequiresApproval,
    string CreatorName,
    string CreatorEmail);

/// <summary>The step at which a participant's RefTest could not be created.</summary>
public enum CreateRefTestsFailureStage
{
    /// <summary>Building the RefTest failed (invalid data or no questions available).</summary>
    Build,

    /// <summary>The approval notification could not be staged; no RefTest of the batch was saved.</summary>
    ApprovalNotification,

    /// <summary>The invitation could not be staged; this RefTest was not saved.</summary>
    Invitation
}

public sealed record CreateRefTestsFailure(
    RefTestParticipant Participant,
    CreateRefTestsFailureStage Stage,
    Exception Exception,
    Guid? RefTestId = null);

/// <param name="Created">The RefTests that were saved, in request order.</param>
/// <param name="Failures">Every participant that did not get a saved RefTest, in the order they failed.</param>
public sealed record CreateRefTestsOutcome(
    IReadOnlyList<RefTest> Created,
    IReadOnlyList<CreateRefTestsFailure> Failures);

/// <summary>
/// Creates RefTests for a batch of participants. A participant whose RefTest cannot be built or
/// whose invitation cannot be staged is reported and skipped; the rest are saved together with
/// their jobs in a single <see cref="IRefTestUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public sealed class CreateRefTestsHandler(
    IIhfRulesQuestionsService questionsService,
    IRefTestSubscriptionService subscriptionService,
    TimeProvider timeProvider)
{
    public async Task<CreateRefTestsOutcome> HandleAsync(
        CreateRefTestsCommand command,
        IRefTestUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var failures = new List<CreateRefTestsFailure>();
        var sharedQuestionIds = await ResolveSharedQuestionIdsAsync(command, cancellationToken);
        var refTests = await BuildRefTestsAsync(command, sharedQuestionIds, failures, cancellationToken);

        if (refTests.Count == 0)
            return new CreateRefTestsOutcome([], failures);

        if (command.RequiresApproval)
        {
            var discard = unitOfWork.BeginJobStaging();
            try
            {
                await unitOfWork.StageApprovalNotificationAsync(ApprovalPayload(command, refTests), cancellationToken);
            }
            catch (Exception ex) when (IsFailure(ex, cancellationToken))
            {
                discard();
                failures.AddRange(refTests.Select(refTest =>
                    new CreateRefTestsFailure(Participant(refTest), CreateRefTestsFailureStage.ApprovalNotification, ex)));
                return new CreateRefTestsOutcome([], failures);
            }
        }
        else if (command.SendAutomatedInvitations)
        {
            await StageInvitationsAsync(refTests, unitOfWork, failures, cancellationToken);
            if (refTests.Count == 0)
                return new CreateRefTestsOutcome([], failures);
        }

        // Payloads were built before the save: RefTest ids are domain-generated, not database-generated.
        unitOfWork.AddRefTests(refTests);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var refTest in refTests)
            await subscriptionService.PublishRefTestCreatedAsync(
                refTest.Id, refTest.FullName, refTest.Email,
                command.TitleId, command.TitleValue,
                refTest.InvitationSentAt.HasValue, refTest.ResultsSentAt.HasValue,
                refTest.SendInvitationsAutomatically, refTest.SendResultsAutomatically,
                refTest.Status, refTest.NumberOfQuestions, refTest.MaxTimeInMinutes,
                refTest.FirstName, refTest.LastName, refTest.CreatedAt, refTest.ScheduledAt,
                cancellationToken);

        return new CreateRefTestsOutcome(refTests, failures);
    }

    /// <summary>
    /// Question IDs shared by every RefTest. Specific question numbers take precedence over
    /// <see cref="CreateRefTestsCommand.RandomQuestionsForEachUser"/>.
    /// </summary>
    private async Task<List<string>> ResolveSharedQuestionIdsAsync(
        CreateRefTestsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SpecificQuestionNumbers is not null)
            return await questionsService.GetQuestionIdsByNumberAsync(
                [.. command.SpecificQuestionNumbers], cancellationToken);

        if (!command.RandomQuestionsForEachUser)
            return await questionsService.GetRandomQuestionIdsAsync(command.NumberOfQuestions, cancellationToken);

        return [];
    }

    private async Task<List<RefTest>> BuildRefTestsAsync(
        CreateRefTestsCommand command,
        List<string> sharedQuestionIds,
        List<CreateRefTestsFailure> failures,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var created = new List<RefTest>();

        foreach (var participant in command.Participants)
        {
            try
            {
                var questionIds = command.RandomQuestionsForEachUser
                    ? await questionsService.GetRandomQuestionIdsAsync(command.NumberOfQuestions, cancellationToken)
                    : sharedQuestionIds;

                created.Add(RefTest.Create(
                    command.TitleId,
                    participant.FirstName, participant.LastName, participant.Email,
                    command.NumberOfQuestions, command.MaxTimeInMinutes,
                    questionIds,
                    command.SendAutomatedInvitations, command.SendAutomatedResults,
                    requiresApproval: command.RequiresApproval,
                    scheduledAt: command.ScheduledAt,
                    creatorName: command.CreatorName,
                    creatorEmail: command.CreatorEmail,
                    now: now));
            }
            catch (Exception ex) when (IsFailure(ex, cancellationToken))
            {
                failures.Add(new CreateRefTestsFailure(participant, CreateRefTestsFailureStage.Build, ex));
            }
        }

        return created;
    }

    /// <summary>
    /// Stages one invitation per RefTest. A RefTest whose invitation cannot be staged is dropped,
    /// together with any job it partly staged, so it is not saved without its invitation.
    /// </summary>
    private static async Task StageInvitationsAsync(
        List<RefTest> refTests,
        IRefTestUnitOfWork unitOfWork,
        List<CreateRefTestsFailure> failures,
        CancellationToken cancellationToken)
    {
        foreach (var refTest in refTests.ToList())
        {
            var discard = unitOfWork.BeginJobStaging();
            try
            {
                await unitOfWork.StageInvitationEmailAsync(refTest, refTest.ScheduledAt, cancellationToken);
            }
            catch (Exception ex) when (IsFailure(ex, cancellationToken))
            {
                discard();
                refTests.Remove(refTest);
                failures.Add(new CreateRefTestsFailure(
                    Participant(refTest), CreateRefTestsFailureStage.Invitation, ex, refTest.Id));
            }
        }
    }

    private static ApprovalNotificationEmailPayload ApprovalPayload(
        CreateRefTestsCommand command,
        List<RefTest> refTests) =>
        new(command.CreatorName, command.CreatorEmail, command.TitleValue,
            [.. refTests.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id, rt.FirstName, rt.LastName, rt.Email, rt.ScheduledAt))]);

    private static RefTestParticipant Participant(RefTest refTest) =>
        new(refTest.FirstName, refTest.LastName, refTest.Email);

    /// <summary>A per-participant failure, as opposed to the caller cancelling the whole request.</summary>
    private static bool IsFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
}
