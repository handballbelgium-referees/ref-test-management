using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;

public interface IRefTestRepository
{
    Task<List<RefTest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    void AddRange(IEnumerable<RefTest> refTests);
}
