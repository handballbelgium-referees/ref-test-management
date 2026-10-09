using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.RefTests.Deletion;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Deletion;

/// <summary>
/// RefTest deletion mutations
/// </summary>
[MutationType]
public static partial class RefTestDeletionMutations
{
    /// <summary>
    /// Delete RefTests. This is an explicit, authorized staff action that permanently deletes
    /// each RefTest record — unlike the public self-service "withdraw consent" flow, which only
    /// redacts personal data and keeps the record. Personal data is redacted from the RefTest's
    /// audit trail first (see <see cref="IRefTestPrivacyErasureService.EraseAsync"/>), so a
    /// staff delete leaves no personal data behind anywhere; the row itself is then removed
    /// (see <see cref="IRefTestPrivacyErasureService.EraseAndDeleteAsync"/>).
    /// </summary>
    /// <param name="input">The requested RefTest IDs.</param>
    /// <param name="handler">Application handler for RefTest deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the deletion operation.</returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    [Authorize(Policy = Permissions.RefTests.Delete)]
    public static async Task<DeleteRefTestsResult> DeleteRefTestsAsync(
        DeleteRefTestsInput input,
        [Service] RefTestDeletionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteRefTestsCommand(input.Ids), cancellationToken);
        return new DeleteRefTestsResult
        {
            TotalRequested = result.TotalRequested,
            SuccessfullyDeleted = result.SuccessfullyDeleted,
            Failed = result.Failed,
            DeletedRefTests = result.DeletedRefTests.Select(snapshot => snapshot.ToDto()).ToList(),
            Errors = result.Errors.Select(error => new DeleteRefTestError
            {
                RefTestId = error.RefTestId,
                ErrorMessage = error.ErrorMessage
            }).ToList()
        };
    }
}
