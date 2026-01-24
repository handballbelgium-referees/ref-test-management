using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Deletion;

/// <summary>
/// RefTest deletion mutations
/// </summary>
[MutationType]
public static class RefTestDeletionMutations
{
    /// <summary>
    /// Delete RefTests
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    [Authorize]
    public static async Task<DeleteRefTestsResult> DeleteRefTestsAsync(
        DeleteRefTestsInput input,
        RefTestManagementContext context,
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

                context.RefTests.Remove(refTest);
                result.SuccessfullyDeleted++;
                result.DeletedRefTests.Add(refTest.ToDto());
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

        await context.SaveChangesAsync(cancellationToken);

        return result;
    }
}

