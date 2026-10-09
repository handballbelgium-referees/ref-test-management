using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Persistence;

public sealed class RefTestRepository(RefTestManagementContext context) : IRefTestRepository
{
    public Task<List<RefTest>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        context.RefTests.Where(refTest => ids.Contains(refTest.Id)).ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<RefTest> refTests) => context.RefTests.AddRange(refTests);
}
