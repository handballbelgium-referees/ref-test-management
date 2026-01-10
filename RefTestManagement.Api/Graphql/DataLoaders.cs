using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

public static class DataLoaders
{
    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, RefTest>> GetRefTestById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<RefTestManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RefTests
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, RefTestTitle>> GetRefTestTitleById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<RefTestManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RefTestTitles
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }
}