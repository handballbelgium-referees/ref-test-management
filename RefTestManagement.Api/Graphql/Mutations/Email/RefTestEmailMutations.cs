using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Application.RefTests.Email;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;

/// <summary>
/// RefTest email mutations (send invitations, results, and reports)
/// </summary>
[MutationType]
public static partial class RefTestEmailMutations
{
    /// <summary>
    /// Send RefTest invitation emails
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to send invitations for.</param>
    /// <param name="handler">Application handler for sending invitation emails.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.SendInvitations)]
    public static async Task<SendInvitationsResult> SendInvitationsAsync(
        SendInvitationsInput input,
        [Service] RefTestEmailHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.SendInvitationsAsync(new SendInvitationsCommand(input.Ids), cancellationToken);
        return new SendInvitationsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullySent = result.SuccessfullySent,
            Failed = result.Failed,
            SentRefTests = result.SentRefTests.Select(refTest => refTest.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new SendInvitationError
            {
                RefTestId = error.RefTestId,
                User = error.RefTest is null
                    ? null
                    : new User(error.RefTest.FirstName, error.RefTest.LastName, error.RefTest.Email),
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }

    /// <summary>
    /// Send RefTest results emails
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to send results for.</param>
    /// <param name="handler">Application handler for sending result emails.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    [Authorize(Policy = Permissions.RefTests.SendResults)]
    public static async Task<SendResultsResult> SendResultsAsync(
        SendResultsInput input,
        [Service] RefTestEmailHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.SendResultsAsync(new SendResultsCommand(input.Ids), cancellationToken);
        return new SendResultsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullySent = result.SuccessfullySent,
            Failed = result.Failed,
            SentRefTests = result.SentRefTests.Select(refTest => refTest.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new SendResultError
            {
                RefTestId = error.RefTestId,
                User = error.RefTest is null
                    ? null
                    : new User(error.RefTest.FirstName, error.RefTest.LastName, error.RefTest.Email),
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }

    /// <summary>
    /// Send RefTest report email
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to send the report for.</param>
    /// <param name="handler">Application handler for creating and sending report emails.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.SendReport)]
    public static async Task<SendReportResult> SendReportAsync(
        SendReportInput input,
        [Service] RefTestEmailHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.SendReportAsync(new SendReportCommand(input.Ids), cancellationToken);
        return new SendReportResult
        {
            Success = result.Success,
            Message = result.Message,
            RefTestCount = result.RefTestCount
        };
    }
}
