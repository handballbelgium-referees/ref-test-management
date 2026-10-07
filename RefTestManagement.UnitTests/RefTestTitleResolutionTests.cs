using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestTitleResolutionTests
{
    [Fact]
    public async Task ResolveOrCreateAsync_ReusesExistingTitleWhenNameDiffersOnlyByCase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var existingTitle = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(existingTitle);
        await context.SaveChangesAsync(cancellationToken);

        var resolvedTitle = await RefTestTitleResolution.ResolveOrCreateAsync(
            context,
            "season 2026",
            cancellationToken);

        Assert.Equal(existingTitle.Id, resolvedTitle.Id);
        Assert.Equal("Season 2026", resolvedTitle.Value);
        Assert.Equal(1, await context.RefTestTitles.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task ResolveOrCreateAsync_CreatesTitleWhenNoCaseInsensitiveMatchExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();

        var resolvedTitle = await RefTestTitleResolution.ResolveOrCreateAsync(
            context,
            "New season",
            cancellationToken);

        Assert.Equal("New season", resolvedTitle.Value);
        Assert.Equal(1, await context.RefTestTitles.CountAsync(cancellationToken));
    }
}
