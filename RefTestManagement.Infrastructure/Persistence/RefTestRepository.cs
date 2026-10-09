using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Persistence;

public sealed class RefTestRepository(
    RefTestManagementContext context,
    IRefTestSessionTokenService sessionTokenService) : IRefTestRepository
{
    public async Task<RefTest?> FindByParticipantCredentialAsync(
        string credential,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var refTests = asNoTracking ? context.RefTests.AsNoTracking() : context.RefTests;
        if (RefTest.IsValidTokenFormat(credential))
        {
            var tokenHash = RefTest.HashToken(credential);
            return await refTests.FirstOrDefaultAsync(
                refTest => refTest.Token == tokenHash, cancellationToken);
        }

        if (!sessionTokenService.TryUnprotect(credential, out var claims) || claims is null)
            return null;

        var refTest = await refTests.FirstOrDefaultAsync(
            candidate => candidate.Id == claims.RefTestId && candidate.Token == claims.InvitationTokenHash,
            cancellationToken);
        return refTest is not null && sessionTokenService.IsValidFor(claims, refTest) ? refTest : null;
    }

    public Task<List<RefTest>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        context.RefTests.Where(refTest => ids.Contains(refTest.Id)).ToListAsync(cancellationToken);

    public Task<List<RefTest>> GetOrderedForReportAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        context.RefTests
            .Include(refTest => refTest.Title)
            .Where(refTest => ids.Contains(refTest.Id))
            .OrderBy(refTest => refTest.LastName)
            .ToListAsync(cancellationToken);

    public Task<RefTest?> ReloadIfDetachedAsync(
        RefTest refTest,
        CancellationToken cancellationToken = default) =>
        context.Entry(refTest).State == EntityState.Detached
            ? FindByIdAsync(refTest.Id, cancellationToken)
            : Task.FromResult<RefTest?>(refTest);

    public Task<RefTest?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RefTests.FirstOrDefaultAsync(refTest => refTest.Id == id, cancellationToken);

    public async Task LoadTitleAsync(RefTest refTest, CancellationToken cancellationToken = default) =>
        await context.Entry(refTest).Reference(candidate => candidate.Title).LoadAsync(cancellationToken);

    public void AddRange(IEnumerable<RefTest> refTests) => context.RefTests.AddRange(refTests);

}
