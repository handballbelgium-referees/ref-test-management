using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

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
    /// (see <see cref="IRefTestPrivacyErasureService.DeleteAsync"/>).
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="privacyErasureService"></param>
    /// <param name="subscriptionService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    [Authorize(Policy = Permissions.RefTests.Delete)]
    public static async Task<DeleteRefTestsResult> DeleteRefTestsAsync(
        DeleteRefTestsInput input,
        RefTestManagementContext context,
        [Service] IRefTestPrivacyErasureService privacyErasureService,
        [Service] IRefTestSubscriptionService subscriptionService,
        CancellationToken cancellationToken)
    {
        var refTests = await context.RefTests
            .Where(s => input.Ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var result = new DeleteRefTestsResult
        {
            TotalRequested = input.Ids.Count,
            DeletedRefTests = [],
            Errors = []
        };

        foreach (var id in input.Ids)
        {
            var refTest = refTests.FirstOrDefault(x => x.Id == id);

            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id.ToString());

                // Capture the DTO before erasing: EraseAsync anonymizes the entity in memory,
                // so reading it afterwards would return redacted placeholders instead of the
                // participant's details the caller expects back.
                var deletedDto = refTest.ToDto();

                // Redact personal data from the audit trail before removing the row. DeleteAsync
                // only removes the RefTest record; audit events carry no FK to it and would
                // otherwise retain the participant's name and email until audit retention
                // expires them.
                if (!refTest.IsAnonymized)
                    await privacyErasureService.EraseAsync(refTest, cancellationToken);

                await privacyErasureService.DeleteAsync(refTest, cancellationToken);

                result.SuccessfullyDeleted++;
                result.DeletedRefTests.Add(deletedDto);
            }
            catch (Exception e)
            {
                result.Failed++;
                result.Errors.Add(new DeleteRefTestError
                {
                    RefTestId = id,
                    ErrorMessage = e.Message
                });
            }
        }

        foreach (var deletedDto in result.DeletedRefTests)
        {
            await subscriptionService.PublishRefTestDeletedAsync(deletedDto.Id, deletedDto.Status, cancellationToken);
        }

        return result;
    }
}

