using Handball.Belgium.RefTestManagement.Api.Extensions;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

[MutationType]
public static partial class RefTestCreationMutations
{
    /// <summary>
    /// Create RefTests for one or more users with the same configuration.
    /// Depending on the caller's permissions, the created RefTests may require approval before invitations can be sent.
    /// </summary>
    /// <param name="input">The input parameters for RefTest creation.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="ihfRulesQuestionsService">Service for fetching IHF Rules questions.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="subscriptionService">Service for managing subscriptions to RefTest creation events.</param>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The result of the RefTest creation operation.</returns>
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
        var requiresApproval = !callerPermissions.Contains(Permissions.RefTests.Approve) && !callerPermissions.Contains(Permissions.RefTests.All) && !callerPermissions.Contains(Permissions.Superadmin);
        var creatorName = currentUser.GetDisplayName();
        var creatorEmail = currentUser.GetEmail();
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var result = new CreateRefTestsResult { TotalRequested = input.Users.Count };

        var (titleId, titleValue) = await ResolveTitleAsync(input.Title, context, cancellationToken);
        var specifiedQuestionIds =
            await ResolveSharedQuestionIdsAsync(input, ihfRulesQuestionsService, cancellationToken);
        var createdRefTests = await BuildRefTestsAsync(input, titleId, specifiedQuestionIds, requiresApproval,
            creatorName, creatorEmail, ihfRulesQuestionsService, result, logger, correlationId, cancellationToken);

        if (createdRefTests.Count == 0)
            return result;

        context.RefTests.AddRange(createdRefTests);

        // The RefTests and the jobs they owe are staged together and committed by the single
        // SaveChanges below, so a persisted RefTest can never exist without its invitation or
        // approval-notification job. Payloads can be built before the save because ids are
        // domain-generated, not database-generated.
        if (requiresApproval)
            await EnqueueApprovalNotificationAsync(createdRefTests, creatorName, creatorEmail, titleValue,
                jobEnqueueService, result, logger, correlationId, cancellationToken);
        else if (input.SendAutomatedInvitations)
            await EnqueueInvitationEmailsAsync(createdRefTests, jobEnqueueService, result, logger, correlationId,
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
    /// If Title.Id is provided, it is used. Otherwise, a new title is created with the provided Name.
    /// </summary>
    /// <param name="titleInput">The input parameters for title resolution.</param>
    /// <param name="context">The database context for accessing RefTestTitles.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>A tuple containing the title ID and value.</returns>
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
    /// Resolve question IDs to be shared among all RefTests.
    /// If SpecificQuestionNumbers are provided, it takes precedence over RandomQuestionsForEachUser.
    /// </summary>
    /// <param name="input">The input parameters for question resolution.</param>
    /// <param name="ihfRulesQuestionsService">Service for fetching IHF Rules questions.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>A list of question IDs to be shared among RefTests.</returns>
    private static async Task<List<string>> ResolveSharedQuestionIdsAsync(
        CreateRefTestsInput input,
        IIhfRulesQuestionsService ihfRulesQuestionsService,
        CancellationToken cancellationToken)
    {
        if (input.SpecificQuestionNumbers is not null)
            return await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(
                input.SpecificQuestionNumbers, cancellationToken);

        if (!input.RandomQuestionsForEachUser)
            return await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(
                input.NumberOfQuestions, cancellationToken);

        return [];
    }

    /// <summary>
    /// Build RefTests for each user. If RandomQuestionsForEachUser is true, each RefTest gets its own set of random questions; otherwise, all RefTests share the same question IDs.
    /// </summary>
    /// <param name="input">The input containing RefTest creation details.</param>
    /// <param name="titleId">The ID of the RefTest title.</param>
    /// <param name="sharedQuestionIds">The list of question IDs to be shared among RefTests.</param>
    /// <param name="requiresApproval">Indicates if RefTests require approval.</param>
    /// <param name="creatorName">The name of the RefTest creator.</param>
    /// <param name="creatorEmail">The email of the RefTest creator.</param>
    /// <param name="ihfRulesQuestionsService">Service for managing IHF rules questions.</param>
    /// <param name="result">The result object for tracking creation status.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>A list of created RefTests.</returns>
    private static async Task<List<RefTest>> BuildRefTestsAsync(
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

        foreach (var user in input.Users)
        {
            try
            {
                var questionIds = input.RandomQuestionsForEachUser
                    ? await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions,
                        cancellationToken)
                    : sharedQuestionIds;

                created.Add(RefTest.Create(
                    titleId,
                    user.FirstName, user.LastName, user.Email,
                    input.NumberOfQuestions, input.MaxTimeInMinutes,
                    questionIds,
                    input.SendAutomatedInvitations, input.SendAutomatedResults,
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
                    User = user,
                    ErrorMessage = MutationErrorHandling.GetUserSafeMessage(ex)
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(CreateRefTestsAsync), correlationId, null);
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
                refTest.Id, refTest.FullName, refTest.Email,
                titleId, titleValue,
                refTest.InvitationSentAt.HasValue, refTest.ResultsSentAt.HasValue,
                refTest.SendInvitationsAutomatically, refTest.SendResultsAutomatically,
                refTest.Status, refTest.NumberOfQuestions, refTest.MaxTimeInMinutes,
                cancellationToken);
    }

    /// <summary>
    /// Stage the approval notification email for all created RefTests. The notification is sent to the creator and includes details of all RefTests awaiting approval.
    /// The job row is added to the same unit of work as the RefTests, so it is committed with them or not at all.
    /// </summary>
    /// <param name="refTests">The list of created RefTests.</param>
    /// <param name="creatorName">The name of the RefTest creator.</param>
    /// <param name="creatorEmail">The email address of the RefTest creator.</param>
    /// <param name="titleValue">The value of the RefTest title.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="result">The result object for tracking operation status.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    private static async Task EnqueueApprovalNotificationAsync(
        List<RefTest> refTests,
        string creatorName,
        string creatorEmail,
        string? titleValue,
        IJobEnqueueService jobEnqueueService,
        CreateRefTestsResult result,
        ILogger logger,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var payload = new ApprovalNotificationEmailPayload(
            creatorName, creatorEmail, titleValue,
            refTests.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id, rt.FirstName, rt.LastName, rt.Email, rt.ScheduledAt)).ToList());

        try
        {
            await jobEnqueueService.EnqueueApprovalNotificationAsync(payload, cancellationToken,
                saveChanges: false);
        }
        catch (Exception ex)
        {
            result.Errors.Add(new CreateRefTestsError
            {
                User = new User(creatorName, string.Empty, creatorEmail),
                ErrorMessage = $"Approval notification could not be prepared: {MutationErrorHandling.GetUserSafeMessage(ex)}"
            });
            MutationErrorHandling.LogMutationFailure(logger, ex, nameof(CreateRefTestsAsync), correlationId, null);
        }
    }

    /// <summary>
    /// Stage invitation emails for all created RefTests.
    /// If SendAutomatedInvitations is false, the RefTests are created but the emails are not sent.
    /// The job rows are added to the same unit of work as the RefTests.
    /// </summary>
    /// <param name="refTests">The list of created RefTests.</param>
    /// <param name="jobEnqueueService">Service for enqueuing job notifications.</param>
    /// <param name="result">The result object for tracking operation status.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    private static async Task EnqueueInvitationEmailsAsync(
        List<RefTest> refTests,
        IJobEnqueueService jobEnqueueService,
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
                        refTest.Id, refTest.FullName, refTest.Email,
                        refTest.Token, refTest.NumberOfQuestions, refTest.MaxTimeInMinutes),
                    executeAfter: refTest.ScheduledAt,
                    cancellationToken: cancellationToken,
                    saveChanges: false);

                result.CreatedRefTests.Add(refTest.ToDto());
            }
            catch (Exception ex)
            {
                result.Errors.Add(new CreateRefTestsError
                {
                    User = new User(refTest.FirstName, refTest.LastName, refTest.Email),
                    ErrorMessage = $"Invitation email could not be prepared: {MutationErrorHandling.GetUserSafeMessage(ex)}"
                });
                MutationErrorHandling.LogMutationFailure(logger, ex, nameof(CreateRefTestsAsync), correlationId, refTest.Id);
            }
        }
    }
}
