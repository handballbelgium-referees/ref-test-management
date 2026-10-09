using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.RefTests.Reset;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

/// <summary>
/// RefTest reset mutations
/// </summary>
[MutationType]
public static partial class RefTestResetMutations
{
    /// <summary>
    /// Reset one or more RefTests to allow retake. Soft reset preserves the audit trail, hard reset clears everything.
    /// </summary>
    /// <param name="input">The input containing the IDs of the RefTests to reset and the reset type.</param>
    /// <param name="handler">Application handler for reset operations.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Reset)]
    public static async Task<ResetRefTestsResult> ResetRefTestsAsync(
        ResetRefTestsInput input,
        [Service] RefTestResetHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ResetAsync(input.Ids, input.ResetType, input.RegenerateToken, cancellationToken);
        return new ResetRefTestsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullyReset = result.SuccessfullyReset,
            Failed = result.TotalRequested - result.SuccessfullyReset,
            ResetRefTests = result.ResetRefTests.Select(refTest => refTest.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new ResetRefTestsError
            {
                RefTestId = error.RefTestId,
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }

    /// <summary>
    /// Revive one or more expired RefTests by resetting them to Pending status with a new token and fresh expiration timer
    /// </summary>
    /// <param name="ids">The IDs of the RefTests to revive.</param>
    /// <param name="handler">Application handler for reset operations.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.RefTests.Revive)]
    public static async Task<ReviveRefTestsResult> ReviveRefTestsAsync(
        [ID<RefTestDto>] List<Guid> ids,
        [Service] RefTestResetHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ReviveAsync(ids, cancellationToken);
        return new ReviveRefTestsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullyRevived = result.SuccessfullyRevived,
            Failed = result.TotalRequested - result.SuccessfullyRevived,
            RevivedRefTests = result.RevivedRefTests.Select(refTest => refTest.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new ReviveRefTestsError
            {
                RefTestId = error.RefTestId,
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }
}
