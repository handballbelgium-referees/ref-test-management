using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IPrivacyNoticeAcceptanceUnitOfWork
{
    Task<RefTest?> FindByCredentialAsync(string credential, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
