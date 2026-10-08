using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

internal static class RefTestTitleResolution
{
    public static async Task<RefTestTitle> ResolveOrCreateAsync(
        RefTestManagementContext context,
        string value,
        CancellationToken cancellationToken)
    {
        var normalizedValue = value.ToLowerInvariant();
        var existingTitle = await context.RefTestTitles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                title => title.Value.ToLower() == normalizedValue,
                cancellationToken);
        if (existingTitle is not null)
            return existingTitle;

        var title = RefTestTitle.Create(value);
        context.RefTestTitles.Add(title);
        await context.SaveChangesWithRetryAsync(cancellationToken);
        return title;
    }
}
