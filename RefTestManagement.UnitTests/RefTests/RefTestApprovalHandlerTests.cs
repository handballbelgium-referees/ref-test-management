using System.Reflection;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>Approve and reject use cases against an in-memory unit of work.</summary>
public sealed class RefTestApprovalHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefTest PendingApproval(string email, bool sendInvitations = false, string creatorEmail = "creator@example.org") =>
        RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", email, 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: sendInvitations, sendResultsAutomatically: false,
            requiresApproval: true, creatorName: "Creator", creatorEmail: creatorEmail, now: Now.AddDays(-1));

    private static (ApproveRefTestsHandler, SubscriptionCounter) Approver()
    {
        var (service, counter) = SubscriptionCounter.Create();
        return (new ApproveRefTestsHandler(service, new FixedTime(Now)), counter);
    }

    [Fact]
    public async Task ApprovingStagesInvitationsAndOneDecisionEmailPerCreatorThenSavesOnce()
    {
        var withInvitation = PendingApproval("a@example.org", sendInvitations: true);
        var withoutInvitation = PendingApproval("b@example.org");
        var otherCreator = PendingApproval("c@example.org", creatorEmail: "other@example.org");
        var unitOfWork = new FakeUnitOfWork(withInvitation, withoutInvitation, otherCreator);
        var (handler, counter) = Approver();

        var outcome = await handler.HandleAsync(
            [withInvitation.Id, withoutInvitation.Id, otherCreator.Id], "Approver", unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Failures);
        Assert.All(outcome.Approved, refTest => Assert.Equal(RefTestStatus.Pending, refTest.Status));
        Assert.Equal(
            [$"invitation:{withInvitation.Id}", "decision:creator@example.org:2:True", "decision:other@example.org:1:True"],
            unitOfWork.Jobs);
        Assert.Equal(1, unitOfWork.Saves);
        Assert.Equal(3, counter.Count);
    }

    [Fact]
    public async Task AFailedInvitationUndoesThatApprovalOnly()
    {
        var failing = PendingApproval("fail@example.org", sendInvitations: true);
        var fine = PendingApproval("ok@example.org");
        var unitOfWork = new FakeUnitOfWork(failing, fine) { FailInvitationFor = failing.Id };
        var (handler, _) = Approver();

        var outcome = await handler.HandleAsync([failing.Id, fine.Id], "Approver", unitOfWork, CancellationToken.None);

        Assert.Equal(fine, Assert.Single(outcome.Approved));
        var failure = Assert.Single(outcome.Failures);
        Assert.Equal(failing.Id, failure.RefTestId);
        Assert.True(failure.DuringInvitation);
        Assert.Equal([failing.Id], unitOfWork.Reverted);
        Assert.DoesNotContain(unitOfWork.Jobs, job => job.StartsWith("invitation:"));
        Assert.Equal(1, unitOfWork.Saves);
    }

    [Fact]
    public async Task UnknownAndRepeatedIdsFailWithoutUndoingTheFirstApproval()
    {
        var refTest = PendingApproval("a@example.org");
        var unknown = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork(refTest);
        var (handler, _) = Approver();

        var outcome = await handler.HandleAsync([refTest.Id, refTest.Id, unknown], "Approver", unitOfWork, CancellationToken.None);

        Assert.Equal(refTest, Assert.Single(outcome.Approved));
        Assert.Collection(outcome.Failures,
            f => Assert.IsType<InvalidOperationException>(f.Exception),
            f => Assert.IsType<RefTestNotFoundException>(f.Exception));
        Assert.Empty(unitOfWork.Reverted);
    }

    [Fact]
    public async Task NothingApprovedMeansNothingSaved()
    {
        var unitOfWork = new FakeUnitOfWork();
        var (handler, counter) = Approver();

        var outcome = await handler.HandleAsync([Guid.NewGuid()], "Approver", unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Approved);
        Assert.Equal(0, unitOfWork.Saves);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task RejectingStagesDecisionEmailsAndReportsInvalidOnes()
    {
        var pending = PendingApproval("a@example.org");
        var approved = PendingApproval("b@example.org");
        approved.Approve(Now);
        var unitOfWork = new FakeUnitOfWork(pending, approved);
        var (service, counter) = SubscriptionCounter.Create();

        var outcome = await new RejectRefTestsHandler(service, new FixedTime(Now))
            .HandleAsync([pending.Id, approved.Id], "Not this season", "Approver", unitOfWork, CancellationToken.None);

        Assert.Equal(pending, Assert.Single(outcome.Rejected));
        Assert.Equal(RefTestStatus.Rejected, pending.Status);
        Assert.IsType<InvalidRefTestStatusException>(Assert.Single(outcome.Failures).Exception);
        Assert.Equal(["decision:creator@example.org:1:False"], unitOfWork.Jobs);
        Assert.Equal(1, unitOfWork.Saves);
        Assert.Equal(1, counter.Count);
    }

    private sealed class FakeUnitOfWork(params RefTest[] stored) : IRefTestUnitOfWork
    {
        public List<string> Jobs { get; } = [];
        public List<Guid> Reverted { get; } = [];
        public int Saves { get; private set; }
        public Guid? FailInvitationFor { get; init; }

        public Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefTest>>([.. stored.Where(refTest => ids.Contains(refTest.Id))]);

        public void AddRefTests(IEnumerable<RefTest> refTests) => throw new NotSupportedException();

        public Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken)
        {
            Reverted.Add(refTest.Id);
            return Task.FromResult<RefTest?>(null);
        }

        public Task StageResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ClearPersonalDataExportRequestsAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RefTest?> DiscardChangesAsync(RefTest refTest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CancelPendingJobsAsync(Guid refTestId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            Jobs.Add($"invitation:{refTest.Id}");
            if (refTest.Id == FailInvitationFor)
                throw new InvalidOperationException("token protection failed");
            return Task.CompletedTask;
        }

        public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken)
        {
            Jobs.Add($"decision:{payload.CreatorEmail}:{payload.RefTests.Count}:{payload.IsApproved}");
            return Task.CompletedTask;
        }

        public Action BeginJobStaging()
        {
            var count = Jobs.Count;
            return () => Jobs.RemoveRange(count, Jobs.Count - count);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    public class SubscriptionCounter : DispatchProxy
    {
        public int Count { get; private set; }

        public static (IRefTestSubscriptionService Service, SubscriptionCounter Counter) Create()
        {
            var service = DispatchProxy.Create<IRefTestSubscriptionService, SubscriptionCounter>();
            return (service, (SubscriptionCounter)(object)service);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name is not (nameof(IRefTestSubscriptionService.PublishRefTestApprovedAsync)
                or nameof(IRefTestSubscriptionService.PublishRefTestRejectedAsync)))
                throw new NotSupportedException(targetMethod?.Name);
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
