using Handball.Belgium.RefTestManagement.Api.Extensions;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Participants;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

[MutationType]
public static partial class RefTestCreationMutations
{
    /// <summary>
    /// Create RefTests for existing participants and/or new participant drafts with the same shared
    /// configuration.
    /// Depending on the caller's permissions, the created RefTests may require approval before
    /// invitations can be sent.
    /// </summary>
    /// <param name="input">The input parameters for RefTest creation.</param>
    /// <param name="context">The database context for accessing RefTests, participants, and titles.</param>
    /// <param name="ihfRulesQuestionsService">Service for fetching IHF Rules questions.</param>
    /// <param name="jobEnqueueService">Service for staging invitation or approval-notification jobs.</param>
    /// <param name="subscriptionService">Service for publishing RefTest creation events.</param>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    [Authorize(Policy = Permissions.RefTests.Create)]
    public static async Task<CreateRefTestsResult> CreateRefTestsAsync(
        CreateRefTestsInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(RefTestCreationMutations));
        var currentUser = httpContextAccessor.HttpContext?.User;
        var callerPermissions = currentUser?.GetPermissions() ?? [];
        var requiresApproval = !callerPermissions.Contains(Permissions.RefTests.Approve)
            && !callerPermissions.Contains(Permissions.RefTests.All)
            && !callerPermissions.Contains(Permissions.Superadmin);
        var creatorName = currentUser.GetDisplayName();
        var creatorEmail = currentUser.GetEmail();
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var result = new CreateRefTestsResult
        {
            TotalRequested = input.ParticipantIds.Count + input.NewParticipants.Count
        };

        var (titleId, titleValue) = await ResolveTitleAsync(input.Title, context, cancellationToken);
        var sharedQuestionIds = await ResolveSharedQuestionIdsAsync(input, ihfRulesQuestionsService, cancellationToken);
        var participantTargets = await ResolveParticipantTargetsAsync(input, context, result, cancellationToken);

        if (participantTargets.CreatedParticipants.Count > 0)
            context.Participants.AddRange(participantTargets.CreatedParticipants);

        var createdRefTests = await BuildRefTestsAsync(
            participantTargets.AllParticipants,
            input,
            titleId,
            sharedQuestionIds,
            requiresApproval,
            creatorName,
            creatorEmail,
            ihfRulesQuestionsService,
            result,
            logger,
            correlationId,
            cancellationToken);

        if (createdRefTests.Count == 0)
            return result;

        context.RefTests.AddRange(createdRefTests);

        // The RefTests, newly-created participants, and any jobs they owe are staged together and
        // committed by the single SaveChanges below, so a persisted RefTest can never exist
        // without its invitation or approval-notification job.
        if (requiresApproval)
            await EnqueueApprovalNotificationAsync(
                createdRefTests,
                creatorName,
                creatorEmail,
                titleValue,
                jobEnqueueService,
                context,
                result,
                logger,
                correlationId,
                cancellationToken);
        else if (input.SendAutomatedInvitations)
            await EnqueueInvitationEmailsAsync(
                createdRefTests,
                jobEnqueueService,
                context,
                result,
                logger,
                correlationId,
                cancellationToken);

        await context.SaveChangesWithRetryAsync(cancellationToken);
        await PublishCreatedEventsAsync(createdRefTests, titleId, titleValue, subscriptionService, cancellationToken);

        if (requiresApproval || !input.SendAutomatedInvitations)
            result.CreatedRefTests.AddRange(createdRefTests.Select(rt => rt.ToDto()));

        return result;
    }

    // --- Helpers ------------------------------------------------------------

    /// <summary>
    /// Resolve RefTest title ID and value from input.
    /// If Title.Id is provided, it is reused. Otherwise, a new title is created with Title.Name.
    /// </summary>
    /// <param name="titleInput">The input parameters for title resolution.</param>
    /// <param name="context">The database context for accessing RefTest titles.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>A tuple containing the resolved title ID and display value.</returns>
    /// <exception cref="ArgumentException">Thrown if neither Title.Id nor Title.Name is provided.</exception>
    private static async Task<(Guid Id, string? Value)> ResolveTitleAsync(
        Title titleInput,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        if (titleInput.Id is not null)
        {
            var existing = await context.RefTestTitles.FindAsync([titleInput.Id.Value], cancellationToken);
            return (titleInput.Id.Value, existing?.Value);
        }

        if (titleInput.Name is null)
            throw new ArgumentException("Either Title.Id or Title.Name must be provided.");

        var title = RefTestTitle.Create(titleInput.Name);
        context.RefTestTitles.Add(title);
        await context.SaveChangesWithRetryAsync(cancellationToken);
        return (title.Id, title.Value);
    }

    /// <summary>
    /// Resolve the question IDs shared among the RefTests in the current request.
    /// Explicit question numbers take precedence; otherwise, one random set is reused unless each
    /// participant asked for their own random draw.
    /// </summary>
    /// <param name="input">The input parameters for question resolution.</param>
    /// <param name="ihfRulesQuestionsService">Service for fetching question IDs from the IHF source.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The shared question IDs, or an empty list when each participant needs their own draw.</returns>
    private static async Task<List<string>> ResolveSharedQuestionIdsAsync(
        CreateRefTestsInput input,
        IIhfRulesQuestionsService ihfRulesQuestionsService,
        CancellationToken cancellationToken)
    {
        if (input.SpecificQuestionNumbers is not null)
            return await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(
                input.SpecificQuestionNumbers,
                cancellationToken);

        if (!input.RandomQuestionsForEachUser)
            return await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(
                input.NumberOfQuestions,
                cancellationToken);

        return [];
    }

    /// <summary>
    /// Resolve the participants referenced by the creation request.
    /// Existing participants are loaded in request order and draft participants are validated,
    /// deduplicated by email, and prepared for insertion.
    /// </summary>
    /// <param name="input">The participant selection and draft data from the request.</param>
    /// <param name="context">The database context for loading and creating participants.</param>
    /// <param name="result">The result object used to record participant-level validation failures.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The ordered participants to create RefTests for, plus any new participants to persist.</returns>
    private static async Task<ResolvedParticipants> ResolveParticipantTargetsAsync(
        CreateRefTestsInput input,
        RefTestManagementContext context,
        CreateRefTestsResult result,
        CancellationToken cancellationToken)
    {
        var existingParticipants = await ResolveExistingParticipantsAsync(input.ParticipantIds, context, cancellationToken);
        var reservedEmails = new HashSet<string>(existingParticipants.Select(participant => participant.Email), StringComparer.Ordinal);
        var createdParticipants = new List<Participant>();
        var allParticipants = new List<Participant>(existingParticipants);

        foreach (var draft in input.NewParticipants)
        {
            try
            {
                var participant = Participant.Create(
                    draft.FirstName,
                    draft.LastName,
                    draft.Email,
                    draft.Type,
                    draft.Level);

                if (!reservedEmails.Add(participant.Email))
                    throw new ArgumentException("Each participant email can only be used once per request.", nameof(draft.Email));

                var emailTaken = await context.Participants
                    .AsNoTracking()
                    .AnyAsync(existing => existing.Email == participant.Email, cancellationToken);

                if (emailTaken)
                    throw new ArgumentException("A participant with this email already exists.", nameof(draft.Email));

                createdParticipants.Add(participant);
                allParticipants.Add(participant);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new CreateRefTestsError
                {
                    Participant = draft,
                    ErrorMessage = MutationErrorHandling.GetUserSafeMessage(ex)
                });
            }
        }

        return new ResolvedParticipants(allParticipants, createdParticipants);
    }

    /// <summary>
    /// Load the existing participants selected for this request while preserving the input order.
    /// </summary>
    /// <param name="participantIds">The participant IDs selected by the caller.</param>
    /// <param name="context">The database context for loading participants.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The resolved participants in the same order as the requested IDs.</returns>
    /// <exception cref="ParticipantNotFoundException">Thrown when any requested participant ID is unknown.</exception>
    private static async Task<List<Participant>> ResolveExistingParticipantsAsync(
        IReadOnlyList<Guid> participantIds,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        if (participantIds.Count == 0)
            return [];

        var participants = await context.Participants
            .AsNoTracking()
            .Where(participant => participantIds.Contains(participant.Id))
            .ToDictionaryAsync(participant => participant.Id, cancellationToken);

        var orderedParticipants = new List<Participant>(participantIds.Count);
        foreach (var participantId in participantIds)
        {
            if (!participants.TryGetValue(participantId, out var participant))
                throw new ParticipantNotFoundException(participantId);

            orderedParticipants.Add(participant);
        }

        return orderedParticipants;
    }

    /// <summary>
    /// Build RefTests for each resolved participant.
    /// If RandomQuestionsForEachUser is true, each RefTest gets its own random set of questions;
    /// otherwise, all RefTests share the same question IDs.
    /// </summary>
    /// <param name="participants">The participants to create RefTests for.</param>
    /// <param name="input">The input containing RefTest creation details.</param>
    /// <param name="titleId">The resolved RefTest title ID.</param>
    /// <param name="sharedQuestionIds">The question IDs shared across the batch when applicable.</param>
    /// <param name="requiresApproval">Indicates if the created RefTests should start in PendingApproval.</param>
    /// <param name="creatorName">The display name of the staff user creating the RefTests.</param>
    /// <param name="creatorEmail">The email of the staff user creating the RefTests.</param>
    /// <param name="ihfRulesQuestionsService">Service for fetching random question IDs.</param>
    /// <param name="result">The result object for tracking per-participant failures.</param>
    /// <param name="logger">The logger for logging failures.</param>
    /// <param name="correlationId">The correlation ID for tracking the operation.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The RefTests that were created successfully.</returns>
    private static async Task<List<RefTest>> BuildRefTestsAsync(
        IReadOnlyList<Participant> participants,
        CreateRefTestsInput input,
        Guid titleId,
        List<string> sharedQuestionIds,
        bool requiresApproval,
        string creatorName,
        string creatorEmail,
        IIhfRulesQuestionsService ihfRulesQuestionsService,
        CreateRefTestsResult result,
        ILogger logger,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var created = new List<RefTest>();

        foreach (var participant in participants)
        {
            try
            {
                var questionIds = input.RandomQuestionsForEachUser
                    ? await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions, cancellationToken)
                    : sharedQuestionIds;

                created.Add(RefTest.Create(
                    titleId,
                    participant.Id,
                    participant.FirstName,
                    participant.LastName,
                    participant.Email,
                    input.NumberOfQuestions,
                    input.MaxTimeInMinutes,
                    questionIds,
                    input.SendAutomatedInvitations,
                    input.SendAutomatedResults,
                    requiresApproval: requiresApproval,
                    scheduledAt: input.ScheduledAt,
                    creatorName: creatorName,
                    creatorEmail: creatorEmail));

                result.SuccessfullyCreated++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new CreateRefTestsError
                {
                    Participant = new ParticipantDraft(
                        participant.FirstName,
                        participant.LastName,
                        participant.Email,
                        participant.Type,
                        participant.Level),
                    ErrorMessage = MutationErrorHandling.GetUserSafeMessage(ex)
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(CreateRefTestsAsync), correlationId);
            }
        }

        return created;
    }

    /// <summary>
    /// Publish RefTestCreated events for all created RefTests.
    /// </summary>
    /// <param name="refTests">The list of created RefTests.</param>
    /// <param name="titleId">The ID of the RefTest title.</param>
    /// <param name="titleValue">The value of the RefTest title.</param>
    /// <param name="subscriptionService">Service for publishing RefTestCreated events.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    private static async Task PublishCreatedEventsAsync(
        List<RefTest> refTests,
        Guid titleId,
        string? titleValue,
        IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        foreach (var refTest in refTests)
            await subscriptionService.PublishRefTestCreatedAsync(
                refTest.Id,
                refTest.FullName,
                refTest.Email,
                titleId,
                titleValue,
                refTest.InvitationSentAt.HasValue,
                refTest.ResultsSentAt.HasValue,
                refTest.SendInvitationsAutomatically,
                refTest.SendResultsAutomatically,
                refTest.Status,
                refTest.NumberOfQuestions,
                refTest.MaxTimeInMinutes,
                cancellationToken);
    }

    /// <summary>
    /// Stage the approval notification email for all created RefTests. The notification is sent to
    /// the creator and includes details of all RefTests awaiting approval.
    /// The job row is added to the same unit of work as the RefTests and any new participants, so
    /// it is committed with them or not at all.
    /// </summary>
    /// <param name="refTests">The list of created RefTests.</param>
    /// <param name="creatorName">The name of the RefTest creator.</param>
    /// <param name="creatorEmail">The email address of the RefTest creator.</param>
    /// <param name="titleValue">The value of the RefTest title.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="result">The result object for tracking operation status.</param>
    /// <param name="logger">The logger for logging information and errors.</param>
    /// <param name="correlationId">The correlation ID for tracking the operation.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    private static async Task EnqueueApprovalNotificationAsync(
        List<RefTest> refTests,
        string creatorName,
        string creatorEmail,
        string? titleValue,
        IJobEnqueueService jobEnqueueService,
        RefTestManagementContext context,
        CreateRefTestsResult result,
        ILogger logger,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var payload = new ApprovalNotificationEmailPayload(
            creatorName,
            creatorEmail,
            titleValue,
            refTests.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id,
                rt.FirstName,
                rt.LastName,
                rt.Email,
                rt.ScheduledAt)).ToList());

        try
        {
            await jobEnqueueService.EnqueueApprovalNotificationAsync(
                payload,
                saveChanges: false,
                unitOfWorkContext: context,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            result.Errors.Add(new CreateRefTestsError
            {
                Participant = new ParticipantDraft(creatorName, string.Empty, creatorEmail, ParticipantType.Other, null),
                ErrorMessage = $"Approval notification could not be prepared: {MutationErrorHandling.GetUserSafeMessage(ex)}"
            });
            MutationErrorHandling.LogMutationFailure(logger, ex, nameof(CreateRefTestsAsync), correlationId);
        }
    }

    /// <summary>
    /// Stage invitation emails for all created RefTests.
    /// If SendAutomatedInvitations is false, the RefTests are created but the emails are not sent.
    /// The job rows are added to the same unit of work as the RefTests.
    /// </summary>
    /// <param name="refTests">The list of created RefTests.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="result">The result object for tracking operation status.</param>
    /// <param name="logger">The logger for logging information and errors.</param>
    /// <param name="correlationId">The correlation ID for tracking the operation.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    private static async Task EnqueueInvitationEmailsAsync(
        List<RefTest> refTests,
        IJobEnqueueService jobEnqueueService,
        RefTestManagementContext context,
        CreateRefTestsResult result,
        ILogger logger,
        string correlationId,
        CancellationToken cancellationToken)
    {
        foreach (var refTest in refTests)
        {
            try
            {
                await jobEnqueueService.EnqueueInvitationEmailAsync(
                    new InvitationEmailPayload(
                        refTest.Id,
                        refTest.FullName,
                        refTest.Email,
                        refTest.Token,
                        refTest.NumberOfQuestions,
                        refTest.MaxTimeInMinutes),
                    executeAfter: refTest.ScheduledAt,
                    saveChanges: false,
                    unitOfWorkContext: context,
                    cancellationToken: cancellationToken);

                result.CreatedRefTests.Add(refTest.ToDto());
            }
            catch (Exception ex)
            {
                result.Errors.Add(new CreateRefTestsError
                {
                    Participant = new ParticipantDraft(
                        refTest.FirstName,
                        refTest.LastName,
                        refTest.Email,
                        ParticipantType.Other,
                        null),
                    ErrorMessage = $"Invitation email could not be prepared: {MutationErrorHandling.GetUserSafeMessage(ex)}"
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(CreateRefTestsAsync), correlationId, refTest.Id);
            }
        }
    }

    private sealed record ResolvedParticipants(
        IReadOnlyList<Participant> AllParticipants,
        IReadOnlyList<Participant> CreatedParticipants);
}
