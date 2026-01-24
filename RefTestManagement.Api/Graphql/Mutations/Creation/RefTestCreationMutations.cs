using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

/// <summary>
/// RefTest creation mutations
/// </summary>
[MutationType]
public static class RefTestCreationMutations
{
    /// <summary>
    /// Create multiple RefTests for multiple users
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static async Task<BulkRefTestsResult> CreateBulkRefTestsAsync(
        CreateBulkRefTestsInput input,
        RefTestManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var result = new BulkRefTestsResult
        {
            TotalRequested = input.Users.Count
        };

        var createdRefTests = new List<RefTest>();

        // Get questionIds from question numbers if specified
        List<string> specifiedQuestionIds = [];
        if (input.SpecificQuestionNumbers is not null)
        {
            specifiedQuestionIds = await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(
                input.SpecificQuestionNumbers,
                cancellationToken);
        }
        else if (input.SpecificQuestionNumbers is null && !input.RandomQuestionsForEachUser)
        {
            specifiedQuestionIds =
                await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions, cancellationToken);
        }

        Guid titleId;

        switch (input.Title.Id)
        {
            case not null:
                titleId = input.Title.Id.Value;
                break;
            case null when input.Title.Name is not null:
            {
                var title = RefTestTitle.Create(input.Title.Name);
                context.RefTestTitles.Add(title);
                await context.SaveChangesAsync(cancellationToken);

                titleId = title.Id;
                break;
            }
            default:
                throw new ArgumentException("Either Title.Id or Title.Name must be provided.");
        }

        foreach (var user in input.Users)
        {
            try
            {
                // If no specific question numbers were specified, get random questions
                var questionIds = specifiedQuestionIds;

                if (input.RandomQuestionsForEachUser)
                {
                    questionIds = await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions,
                        cancellationToken);
                }

                var refTest = RefTest.Create(
                    titleId,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    input.NumberOfQuestions,
                    input.MaxTimeInMinutes,
                    questionIds,
                    input.SendAutomatedInvitations,
                    input.SendAutomatedResults
                );

                createdRefTests.Add(refTest);
                result.SuccessfullyCreated++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new BulkCreationError
                {
                    User = user,
                    ErrorMessage = ex.Message
                });
            }
        }

        // Save all valid RefTests to a database
        if (createdRefTests.Count == 0)
            return result;

        if (!input.SendAutomatedInvitations)
        {
            context.RefTests.AddRange(createdRefTests);
            await context.SaveChangesAsync(cancellationToken);

            return result;
        }

        // Send invitation emails to all participants
        foreach (var refTest in createdRefTests)
        {
            try
            {
                var invitationPayload = new InvitationEmailPayload(
                    refTest.Id,
                    refTest.FullName,
                    refTest.Email,
                    refTest.Token,
                    refTest.NumberOfQuestions,
                    refTest.MaxTimeInMinutes
                );

                await jobEnqueueService.EnqueueInvitationEmailAsync(invitationPayload,
                    cancellationToken: cancellationToken);

                result.CreatedRefTests.Add(refTest.ToDto());
            }
            catch (Exception ex)
            {
                // Email job enqueue failed, but the RefTest was created
                // Log the error but don't fail the entire operation
                result.Errors.Add(new BulkCreationError
                {
                    User = new User(refTest.FirstName, refTest.LastName, refTest.Email),
                    ErrorMessage = $"RefTest created but email job enqueue failed: {ex.Message}"
                });
            }
        }

        context.RefTests.AddRange(createdRefTests);
        await context.SaveChangesAsync(cancellationToken);

        return result;
    }
}

