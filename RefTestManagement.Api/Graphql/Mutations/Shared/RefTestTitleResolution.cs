using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

/// <summary>Compatibility adapter for mutation groups that have not yet moved to Application handlers.</summary>
internal static class RefTestTitleResolution
{
    public static Task<RefTestTitle> ResolveOrCreateAsync(
        IRefTestTitleRepository repository,
        string value,
        CancellationToken cancellationToken) =>
        repository.ResolveOrCreateAsync(value, cancellationToken);
}
