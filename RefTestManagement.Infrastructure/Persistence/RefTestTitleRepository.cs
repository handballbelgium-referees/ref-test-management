using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Persistence;

public sealed class RefTestTitleRepository(RefTestManagementContext context) : IRefTestTitleRepository
{
    public async Task<RefTestTitle?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.RefTestTitles.FindAsync([id], cancellationToken);

    /// <summary>
    /// Resolve RefTest title by its value; compare case-insensitively and save a new title if none exists.
    /// </summary>
    /// <remarks>
    /// Matching is invariant to casing, so a differently-cased duplicate reuses the original
    /// title. A newly created title is saved immediately, as it was before this resolution was
    /// moved behind the persistence port.
    /// </remarks>
    public async Task<RefTestTitle> ResolveOrCreateAsync(
        string value,
        CancellationToken cancellationToken = default)
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
