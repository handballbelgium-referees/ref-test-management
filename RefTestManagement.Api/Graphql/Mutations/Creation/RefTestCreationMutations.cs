using Handball.Belgium.RefTestManagement.Api.Extensions;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests.Creation;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

[MutationType]
public static partial class RefTestCreationMutations
{
    /// <summary>
    /// Create RefTests for one or more users with the same configuration.
    /// Depending on the caller's permissions, the created RefTests may require approval before invitations can be sent.
    /// </summary>
    /// <param name="input">The input parameters for RefTest creation.</param>
    /// <param name="handler">Application handler for RefTest creation.</param>
    /// <param name="currentUser">The current caller and its permission set.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The result of the RefTest creation operation.</returns>
    [Authorize(Policy = Permissions.RefTests.Create)]
    public static async Task<CreateRefTestsResult> CreateRefTestsAsync(
        CreateRefTestsInput input,
        [Service] RefTestCreationHandler handler,
        [Service] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var requiresApproval = !currentUser.Permissions.Contains(Permissions.RefTests.Approve)
                               && !currentUser.Permissions.Contains(Permissions.RefTests.All)
                               && !currentUser.Permissions.Contains(Permissions.Superadmin);
        var result = await handler.HandleAsync(
            new CreateRefTestsCommand(
                input.Title.Id,
                input.Title.Name,
                input.Users.Select(user => new CreateRefTestUser(user.FirstName, user.LastName, user.Email)).ToList(),
                input.NumberOfQuestions,
                input.RandomQuestionsForEachUser,
                input.MaxTimeInMinutes,
                input.SpecificQuestionNumbers,
                input.SendAutomatedInvitations,
                input.SendAutomatedResults,
                input.ScheduledAt,
                requiresApproval),
            cancellationToken);

        return new CreateRefTestsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullyCreated = result.SuccessfullyCreated,
            Failed = result.Failed,
            CreatedRefTests = result.CreatedRefTests.Select(refTest => refTest.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new CreateRefTestsError
            {
                User = new User(error.User.FirstName, error.User.LastName, error.User.Email),
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }
}
