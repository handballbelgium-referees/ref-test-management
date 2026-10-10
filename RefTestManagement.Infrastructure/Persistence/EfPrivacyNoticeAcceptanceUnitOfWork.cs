using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Privacy;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Persistence;

public sealed class EfPrivacyNoticeAcceptanceUnitOfWork(
    RefTestManagementContext context,
    IRefTestSessionTokenService sessionTokenService) : IPrivacyNoticeAcceptanceUnitOfWork
{
    public Task<RefTest?> FindByCredentialAsync(string credential, CancellationToken cancellationToken) =>
        context.RefTests.FindByParticipantCredentialAsync(
            credential,
            sessionTokenService,
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesWithRetryAsync(cancellationToken);
}
