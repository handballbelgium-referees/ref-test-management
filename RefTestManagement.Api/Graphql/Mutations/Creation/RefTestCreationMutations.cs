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

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

[MutationType]
public static class RefTestCreationMutations
{
    [Authorize(Policy = Permissions.RefTests.Create)]
    public static async Task<CreateRefTestsResult> CreateRefTestsAsync(
        CreateRefTestsInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IJobEnqueueService jobEnqueueService,
        [Service] IRefTestSubscriptionService subscriptionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContextAccessor.HttpContext?.User;
        var callerPermissions = currentUser?.FindAll("permissions").Select(c => c.Value).ToHashSet() ?? [];
        var requiresApproval = !callerPermissions.Contains(Permissions.RefTests.Approve);
        var creatorName = currentUser?.FindFirst("name")?.Value
                          ?? currentUser?.FindFirst("email")?.Value
                          ?? "Unknown";
        var creatorEmail = currentUser?.FindFirst("email")?.Value ?? string.Empty;

        var result = new CreateRefTestsResult { TotalRequested = input.Users.Count };

        var (titleId, titleValue) = await ResolveTitleAsync(input.Title, context, cancellationToken);
        var specifiedQuestionIds = await ResolveSharedQuestionIdsAsync(input, ihfRulesQuestionsService, cancellationToken);
        var createdRefTests = await BuildRefTestsAsync(input, titleId, specifiedQuestionIds, requiresApproval,
            ihfRulesQuestionsService, result, cancellationToken);

        if (createdRefTests.Count == 0)
            return result;

        context.RefTests.AddRange(createdRefTests);
        await context.SaveChangesAsync(cancellationToken);

        await PublishCreatedEventsAsync(createdRefTests, titleId, titleValue, subscriptionService, cancellationToken);

        if (requiresApproval)
        {
            await EnqueueApprovalNotificationAsync(createdRefTests, creatorName, creatorEmail, titleValue,
                jobEnqueueService, result, cancellationToken);
            result.CreatedRefTests.AddRange(createdRefTests.Select(rt => rt.ToDto()));
            return result;
        }

        if (input.SendAutomatedInvitations)
            await EnqueueInvitationEmailsAsync(createdRefTests, jobEnqueueService, result, cancellationToken);
        else
            result.CreatedRefTests.AddRange(createdRefTests.Select(rt => rt.ToDto()));

        return result;
    }

    // --- Helpers ------------------------------------------------------------

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

        if (titleInput.Name is not null)
        {
            var title = RefTestTitle.Create(titleInput.Name);
            context.RefTestTitles.Add(title);
            await context.SaveChangesAsync(cancellationToken);
            return (title.Id, title.Value);
        }

        throw new ArgumentException("Either Title.Id or Title.Name must be provided.");
    }

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

    private static async Task<List<RefTest>> BuildRefTestsAsync(
        CreateRefTestsInput input,
        Guid titleId,
        List<string> sharedQuestionIds,
        bool requiresApproval,
        IIhfRulesQuestionsService ihfRulesQuestionsService,
        CreateRefTestsResult result,
        CancellationToken cancellationToken)
    {
        var created = new List<RefTest>();

        foreach (var user in input.Users)
        {
            try
            {
                var questionIds = input.RandomQuestionsForEachUser
                    ? await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions, cancellationToken)
                    : sharedQuestionIds;

                created.Add(RefTest.Create(
                    titleId,
                    user.FirstName, user.LastName, user.Email,
                    input.NumberOfQuestions, input.MaxTimeInMinutes,
                    questionIds,
                    input.SendAutomatedInvitations, input.SendAutomatedResults,
                    requiresApproval: requiresApproval,
                    scheduledAt: input.ScheduledAt));

                result.SuccessfullyCreated++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new CreateRefTestsError { User = user, ErrorMessage = ex.Message });
            }
        }

        return created;
    }

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

    private static async Task EnqueueApprovalNotificationAsync(
        List<RefTest> refTests,
        string creatorName,
        string creatorEmail,
        string? titleValue,
        IJobEnqueueService jobEnqueueService,
        CreateRefTestsResult result,
        CancellationToken cancellationToken)
    {
        var payload = new ApprovalNotificationEmailPayload(
            creatorName, creatorEmail, titleValue,
            refTests.Select(rt => new ApprovalNotificationRefTestItem(
                rt.Id, rt.FirstName, rt.LastName, rt.Email)).ToList());

        try
        {
            await jobEnqueueService.EnqueueApprovalNotificationAsync(payload, cancellationToken);
        }
        catch (Exception ex)
        {
            result.Errors.Add(new CreateRefTestsError
            {
                User = new User(creatorName, string.Empty, creatorEmail),
                ErrorMessage = $"RefTests created but approval notification enqueue failed: {ex.Message}"
            });
        }
    }

    private static async Task EnqueueInvitationEmailsAsync(
        List<RefTest> refTests,
        IJobEnqueueService jobEnqueueService,
        CreateRefTestsResult result,
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
                    cancellationToken: cancellationToken);

                result.CreatedRefTests.Add(refTest.ToDto());
            }
            catch (Exception ex)
            {
                result.Errors.Add(new CreateRefTestsError
                {
                    User = new User(refTest.FirstName, refTest.LastName, refTest.Email),
                    ErrorMessage = $"RefTest created but email job enqueue failed: {ex.Message}"
                });
            }
        }
    }
}

