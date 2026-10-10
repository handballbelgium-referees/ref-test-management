using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class AcceptPrivacyNoticeHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 10, 11, 12, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptsAndPersistsTheNoticeForTheResolvedParticipant()
    {
        var refTest = RefTest.Create(
            Guid.NewGuid(), "Ada", "Lovelace", "ada@example.org", 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: false,
            now: Now.AddHours(-1));
        var unitOfWork = new FakeUnitOfWork(refTest);
        var handler = new AcceptPrivacyNoticeHandler(unitOfWork, new FixedTimeProvider(Now));

        var accepted = await handler.HandleAsync("credential", "v2", CancellationToken.None);

        Assert.Same(refTest, accepted);
        Assert.Equal("credential", unitOfWork.SearchedCredential);
        Assert.Equal("v2", refTest.PrivacyNoticeVersion);
        Assert.Equal(Now, refTest.PrivacyNoticeAcceptedAt);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Contains(refTest.DomainEvents, domainEvent => domainEvent is RefTestPrivacyNoticeAcceptedEvent);
    }

    [Fact]
    public async Task MissingParticipantIsReportedWithoutSaving()
    {
        var unitOfWork = new FakeUnitOfWork(null);
        var handler = new AcceptPrivacyNoticeHandler(unitOfWork, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<RefTestNotFoundException>(
            () => handler.HandleAsync("credential", "v2", CancellationToken.None));

        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private sealed class FakeUnitOfWork(RefTest? refTest) : IPrivacyNoticeAcceptanceUnitOfWork
    {
        public string? SearchedCredential { get; private set; }
        public int SaveCount { get; private set; }

        public Task<RefTest?> FindByCredentialAsync(string credential, CancellationToken cancellationToken)
        {
            SearchedCredential = credential;
            return Task.FromResult(refTest);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
