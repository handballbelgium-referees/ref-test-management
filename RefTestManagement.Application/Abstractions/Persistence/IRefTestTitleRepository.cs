using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;

public interface IRefTestTitleRepository
{
    Task<RefTestTitle?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a title by case-insensitive name, creating and saving it if no match exists.
    /// </summary>
    Task<RefTestTitle> ResolveOrCreateAsync(string value, CancellationToken cancellationToken = default);
}
