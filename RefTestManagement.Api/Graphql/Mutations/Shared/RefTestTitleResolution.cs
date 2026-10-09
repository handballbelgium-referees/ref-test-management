using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

/// <summary>Compatibility adapter for mutation groups that have not yet moved to Application handlers.</summary>
internal static class RefTestTitleResolution
{
    public static Task<Handball.Belgium.RefTestManagement.Domain.RefTestTitles.RefTestTitle> ResolveOrCreateAsync(
        RefTestManagementContext context,
        string value,
        CancellationToken cancellationToken) =>
        new RefTestTitleRepository(context).ResolveOrCreateAsync(value, cancellationToken);
}
