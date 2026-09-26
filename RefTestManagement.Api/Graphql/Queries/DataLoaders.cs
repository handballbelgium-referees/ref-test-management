using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.Participants;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Queries;

public static class DataLoaders
{
    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, RefTestDto>> GetRefTestById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<RefTestManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RefTests
            .AsNoTracking()
            .Include(x => x.Title)
            .Where(x => ids.Contains(x.Id))
            .Select(RefTestMappings.ToDto)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }


    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, ParticipantDto>> GetParticipantById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<RefTestManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Participants
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(ParticipantMappings.ToDto)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, RefTestTitleDto>> GetRefTestTitleById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<RefTestManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RefTestTitles
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(RefTestTitleMappings.ToDto)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }
}
