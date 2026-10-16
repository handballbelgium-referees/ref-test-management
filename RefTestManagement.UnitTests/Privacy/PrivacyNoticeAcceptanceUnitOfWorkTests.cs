using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PrivacyNoticeAcceptanceUnitOfWorkTests
{
    [Fact]
    public async Task FindsByInvitationOrSessionCredentialAndPersistsAggregateChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var sessionTokenService = new RefTestSessionTokenService(
            new EphemeralDataProtectionProvider(),
            TimeProvider.System);

        var refTest = RefTest.Create(
            Guid.NewGuid(), "Ada", "Lovelace", "ada@example.org", 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: false);
        refTest.AcceptPrivacyNotice("v1");
        var invitationToken = refTest.GetIssuedToken();

        await using (var seedContext = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            seedContext.RefTestTitles.Add(title);
            refTest.UpdateTestConfiguration(title.Id, 2, 30, ["q1", "q2"]);
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        var sessionCredential = sessionTokenService.Create(refTest);
        await using (var context = database.CreateContext())
        {
            var unitOfWork = new EfPrivacyNoticeAcceptanceUnitOfWork(context, sessionTokenService);
            var foundByInvitation = await unitOfWork.FindByCredentialAsync(invitationToken, cancellationToken);
            Assert.NotNull(foundByInvitation);
            foundByInvitation.AcceptPrivacyNotice("v2");
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var unitOfWork = new EfPrivacyNoticeAcceptanceUnitOfWork(context, sessionTokenService);
            var foundBySession = await unitOfWork.FindByCredentialAsync(sessionCredential, cancellationToken);
            Assert.NotNull(foundBySession);
            Assert.Equal("v2", foundBySession.PrivacyNoticeVersion);
            Assert.NotNull(foundBySession.PrivacyNoticeAcceptedAt);
        }
    }
}
