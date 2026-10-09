using System.Reflection;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>Staff deletion against an in-memory unit of work and erasure service.</summary>
public sealed class RefTestDeletionHandlerTests
{
    private static RefTest NewRefTest(string email) =>
        RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", email, 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: false);

    [Fact]
    public async Task SnapshotsAreTakenBeforeErasureAndEventsFollowTheDeletes()
    {
        var first = NewRefTest("ada@example.org");
        var failing = NewRefTest("grace@example.org");
        var unknown = Guid.NewGuid();
        var erasure = new FakeErasure { FailFor = failing.Id };
        var (subscriptions, published) = DeletedEvents.Create();

        var outcome = await RefTestDeletionHandler.HandleAsync(
            [first.Id, failing.Id, unknown],
            refTest => refTest.Email,
            new FakeUnitOfWork(first, failing),
            erasure,
            subscriptions,
            CancellationToken.None);

        // The snapshot keeps the address the caller expects back, although erasure redacted it.
        Assert.Equal(["ada@example.org"], outcome.Deleted);
        Assert.Equal("***", first.Email);
        Assert.Collection(outcome.Failures,
            f => Assert.Equal(failing.Id, f.RefTestId),
            f => Assert.IsType<RefTestNotFoundException>(f.Exception));
        Assert.Equal([(first.Id, RefTestStatus.Pending)], published.Events);
    }

    private sealed class FakeErasure : IRefTestPrivacyErasureService
    {
        public Guid? FailFor { get; init; }

        public Task EraseAndDeleteAsync(RefTest refTest, ErasureInitiator initiator, CancellationToken cancellationToken)
        {
            if (refTest.Id == FailFor)
                throw new InvalidOperationException("erasure failed");
            Assert.Equal(ErasureInitiator.Operator, initiator);
            refTest.Anonymize();
            return Task.CompletedTask;
        }

        public Task EraseAsync(RefTest refTest, ErasureInitiator initiator, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> EraseIfDueForRetentionAsync(Guid refTestId, DateTime cutoff, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    public class DeletedEvents : DispatchProxy
    {
        public List<(Guid, RefTestStatus)> Events { get; } = [];

        public static (IRefTestSubscriptionService Service, DeletedEvents Recorder) Create()
        {
            var service = DispatchProxy.Create<IRefTestSubscriptionService, DeletedEvents>();
            return (service, (DeletedEvents)(object)service);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRefTestSubscriptionService.PublishRefTestDeletedAsync))
                throw new NotSupportedException(targetMethod?.Name);
            Events.Add(((Guid)args![0]!, (RefTestStatus)args[1]!));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork(params RefTest[] stored) : IRefTestUnitOfWork
    {
        public Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefTest>>([.. stored.Where(refTest => ids.Contains(refTest.Id))]);

        public void AddRefTests(IEnumerable<RefTest> refTests) => throw new NotSupportedException();
        public Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RefTest?> DiscardChangesAsync(RefTest refTest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelPendingJobsAsync(Guid refTestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageResultEmailAsync(ResultEmailPayload payload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ClearPersonalDataExportRequestsAsync(string email, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Action BeginJobStaging() => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
