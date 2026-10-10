using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

public sealed class AcceptPrivacyNoticeHandler(
    IPrivacyNoticeAcceptanceUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<RefTest> HandleAsync(
        string credential,
        string noticeVersion,
        CancellationToken cancellationToken)
    {
        var refTest = await unitOfWork.FindByCredentialAsync(credential, cancellationToken)
            ?? throw new RefTestNotFoundException();

        refTest.AcceptPrivacyNotice(noticeVersion, timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return refTest;
    }
}
