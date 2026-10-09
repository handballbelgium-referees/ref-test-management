using Handball.Belgium.RefTestManagement.Api.Extensions;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

[MutationType]
public static partial class RefTestCreationMutations
{
    /// <summary>Largest number of users and of questions a single createRefTests request may ask for.</summary>
    /// <remarks>
    /// Each user can cost an upstream question-bank call and a database row, so an unbounded
    /// request lets one caller tie up the API and the IHF service.
    /// </remarks>
    internal const int MaxBatchSize = 200;

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
    /// <param name="loggerFactory">Factory for creating loggers.</param>
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
        CancellationToken cancellationToken,
        [Service] TimeProvider? timeProvider = null)
    {
        if (input.Users.Count > MaxBatchSize || input.NumberOfQuestions > MaxBatchSize)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage($"A single request can create at most {MaxBatchSize} RefTests with at most {MaxBatchSize} questions each.")
                .SetCode("REFTEST_BATCH_TOO_LARGE")
                .Build());

        var logger = loggerFactory.CreateLogger(nameof(RefTestCreationMutations));
        var currentUser = httpContextAccessor.HttpContext?.User;
        var callerPermissions = currentUser?.GetPermissions() ?? [];
        var requiresApproval = !callerPermissions.Contains(Permissions.RefTests.Approve) && !callerPermissions.Contains(Permissions.RefTests.All) && !callerPermissions.Contains(Permissions.Superadmin);
        var creatorName = currentUser.GetDisplayName();
        var creatorEmail = currentUser.GetEmail();
        var correlationId = MutationErrorHandling.GetCorrelationId(httpContextAccessor);

        var result = new CreateRefTestsResult { TotalRequested = input.Users.Count };

        var (titleId, titleValue) = await ResolveTitleAsync(input.Title, context, cancellationToken);

        var handler = new CreateRefTestsHandler(
            ihfRulesQuestionsService, subscriptionService, timeProvider ?? TimeProvider.System);
        var outcome = await handler.HandleAsync(
            new CreateRefTestsCommand(
                [.. input.Users.Select(user => new RefTestParticipant(user.FirstName, user.LastName, user.Email))],
                titleId,
                titleValue,
                input.NumberOfQuestions,
                input.MaxTimeInMinutes,
                input.RandomQuestionsForEachUser,
                input.SpecificQuestionNumbers,
                input.SendAutomatedInvitations,
                input.SendAutomatedResults,
                input.ScheduledAt,
                requiresApproval,
                creatorName,
                creatorEmail),
            new EfRefTestUnitOfWork(context, jobEnqueueService),
            cancellationToken);

        foreach (var failure in outcome.Failures)
        {
            result.Failed++;
            result.Errors.Add(new CreateRefTestsError
            {
                User = new User(failure.Participant.FirstName, failure.Participant.LastName, failure.Participant.Email),
                ErrorMessage = ErrorMessage(failure)
            });
        }

        // An approval failure is one exception shared by the whole batch, so it is logged once.
        var approvalFailureLogged = false;
        foreach (var failure in outcome.Failures)
        {
            if (failure.Stage == CreateRefTestsFailureStage.ApprovalNotification)
            {
                if (approvalFailureLogged)
                    continue;
                approvalFailureLogged = true;
            }

            MutationErrorHandling.LogMutationFailure(
                logger, failure.Exception, nameof(CreateRefTestsAsync), correlationId, failure.RefTestId);
        }

        result.SuccessfullyCreated = outcome.Created.Count;
        result.CreatedRefTests.AddRange(outcome.Created.Select(refTest => refTest.ToDto()));
        return result;
    }

    private static string ErrorMessage(CreateRefTestsFailure failure)
    {
        var message = MutationErrorHandling.GetUserSafeMessage(failure.Exception);
        return failure.Stage switch
        {
            CreateRefTestsFailureStage.ApprovalNotification => $"Approval notification could not be prepared: {message}",
            CreateRefTestsFailureStage.Invitation => $"Invitation email could not be prepared: {message}",
            _ => message
        };
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

        var title = await RefTestTitleResolution.ResolveOrCreateAsync(
            context,
            titleInput.Name,
            cancellationToken);
        return (title.Id, title.Value);
    }
}
