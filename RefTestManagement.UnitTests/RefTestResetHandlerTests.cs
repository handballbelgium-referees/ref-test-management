using System.Reflection;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>Reset and revive use cases against an in-memory unit of work.</summary>
public sealed class RefTestResetHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefTest Completed(bool invitationSent = true)
    {
        var refTest = RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.org", 2, 30,
            ["q1", "q2"], sendInvitationAutomatically: false, sendResultsAutomatically: false, now: Now.AddHours(-2));
        if (invitationSent)
            refTest.SendInvitation(Now.AddHours(-2));
        refTest.AcceptPrivacyNotice("v1", Now.AddHours(-1));
        refTest.Start("v1", Now.AddHours(-1));
        refTest.Complete(1, 1, 2, 50, ["a1"], [], [], now: Now.AddMinutes(-40));
        return refTest;
    }

    private static RefTest Expired(bool invitationSent = true)
    {
        var refTest = RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.org", 2, 30,
            ["q1", "q2"], sendInvitationAutomatically: false, sendResultsAutomatically: false, now: Now.AddDays(-10));
        if (invitationSent)
            refTest.SendInvitation(Now.AddDays(-10));
        refTest.Expire(Now.AddDays(-3));
        return refTest;
    }

    private static (IRefTestSubscriptionService, EventCounter) Events() => EventCounter.Create();

    [Fact]
    public async Task HardResetCancelsAllJobsReinvitesAndSavesPerItem()
    {
        var first = Completed();
        var second = Completed(invitationSent: false);
        var unitOfWork = new FakeUnitOfWork(first, second);
        var (events, counter) = Events();

        var outcome = await new ResetRefTestsHandler(events, new FixedTime(Now))
            .HandleAsync([first.Id, second.Id], RefTestResetType.Hard, regenerateToken: false, unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Failures);
        Assert.All(outcome.Reset, item => Assert.Equal(RefTestStatus.Completed, item.OldStatus));
        Assert.All(outcome.Reset, item => Assert.Equal(RefTestStatus.Pending, item.RefTest.Status));
        Assert.Equal(
            [$"cancel-all:{first.Id}", $"invitation:{first.Id}", "save", $"cancel-all:{second.Id}", "save"],
            unitOfWork.Log);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task SoftResetWithoutNewTokenKeepsTheInvitationAndOnlyCancelsResultEmails()
    {
        var refTest = Completed();
        var unitOfWork = new FakeUnitOfWork(refTest);
        var (events, _) = Events();

        var outcome = await new ResetRefTestsHandler(events, new FixedTime(Now))
            .HandleAsync([refTest.Id], RefTestResetType.Soft, regenerateToken: false, unitOfWork, CancellationToken.None);

        Assert.Single(outcome.Reset);
        Assert.Equal([$"cancel-results:{refTest.Id}", "save"], unitOfWork.Log);
    }

    [Fact]
    public async Task AFailedItemIsRolledBackAndTheBatchContinues()
    {
        var failing = Completed();
        var fine = Completed(invitationSent: false);
        var unitOfWork = new FakeUnitOfWork(failing, fine) { FailInvitationFor = failing.Id };
        var (events, counter) = Events();

        var outcome = await new ResetRefTestsHandler(events, new FixedTime(Now))
            .HandleAsync([failing.Id, fine.Id], RefTestResetType.Hard, regenerateToken: false, unitOfWork, CancellationToken.None);

        Assert.Equal(fine, Assert.Single(outcome.Reset).RefTest);
        Assert.Equal(failing.Id, Assert.Single(outcome.Failures).RefTestId);
        Assert.Contains($"discard:{failing.Id}", unitOfWork.Log);
        Assert.Equal(1, unitOfWork.Log.Count(entry => entry == "save"));
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task ReviveReinvitesExpiredTestsAndRejectsOthers()
    {
        var expired = Expired();
        var completed = Completed();
        var unknown = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork(expired, completed);
        var (events, counter) = Events();

        var outcome = await new ReviveRefTestsHandler(events, new FixedTime(Now))
            .HandleAsync([expired.Id, completed.Id, unknown], unitOfWork, CancellationToken.None);

        var revived = Assert.Single(outcome.Revived);
        Assert.Equal(RefTestStatus.Pending, revived.Status);
        Assert.Equal(Now, revived.CreatedAt);
        Assert.Collection(outcome.Failures,
            f => Assert.IsType<InvalidRefTestStatusException>(f.Exception),
            f => Assert.IsType<RefTestNotFoundException>(f.Exception));
        Assert.Contains($"invitation:{expired.Id}", unitOfWork.Log);
        Assert.Contains($"discard:{completed.Id}", unitOfWork.Log);
        Assert.Equal(1, counter.Count);
    }

    private sealed class FakeUnitOfWork(params RefTest[] stored) : IRefTestUnitOfWork
    {
        public List<string> Log { get; } = [];
        public Guid? FailInvitationFor { get; init; }

        public Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefTest>>([.. stored.Where(refTest => ids.Contains(refTest.Id))]);

        public Task StageResultEmailAsync(ResultEmailPayload payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ClearPersonalDataExportRequestsAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RefTest?> DiscardChangesAsync(RefTest refTest, CancellationToken cancellationToken)
        {
            Log.Add($"discard:{refTest.Id}");
            return Task.FromResult<RefTest?>(null);
        }

        public Task CancelPendingJobsAsync(Guid refTestId, CancellationToken cancellationToken)
        {
            Log.Add($"cancel-all:{refTestId}");
            return Task.CompletedTask;
        }

        public Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken)
        {
            Log.Add($"cancel-results:{refTestId}");
            return Task.CompletedTask;
        }

        public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            if (refTest.Id == FailInvitationFor)
                throw new InvalidOperationException("token protection failed");
            Log.Add($"invitation:{refTest.Id}");
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Log.Add("save");
            return Task.CompletedTask;
        }

        public void AddRefTests(IEnumerable<RefTest> refTests) => throw new NotSupportedException();

        public Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Action BeginJobStaging() => throw new NotSupportedException();
    }

    public class EventCounter : DispatchProxy
    {
        public int Count { get; private set; }

        public static (IRefTestSubscriptionService Service, EventCounter Counter) Create()
        {
            var service = DispatchProxy.Create<IRefTestSubscriptionService, EventCounter>();
            return (service, (EventCounter)(object)service);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name is not (nameof(IRefTestSubscriptionService.PublishRefTestResetAsync)
                or nameof(IRefTestSubscriptionService.PublishRefTestRevivedAsync)))
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
