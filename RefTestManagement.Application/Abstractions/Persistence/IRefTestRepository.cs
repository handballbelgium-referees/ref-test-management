using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;

public interface IRefTestRepository
{
    Task<RefTest?> FindByParticipantCredentialAsync(
        string credential,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default);
    Task<List<RefTest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    void AddRange(IEnumerable<RefTest> refTests);
}
