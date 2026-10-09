using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>On-request invitation and result emails against an in-memory unit of work.</summary>
public sealed class RefTestEmailHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefTest Pending(string email = "ada@example.org") =>
        RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", email, 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: false, now: Now.AddHours(-1));

    private static RefTest Completed()
    {
        var refTest = Pending();
        refTest.AcceptPrivacyNotice("v1", Now.AddMinutes(-50));
        refTest.Start("v1", Now.AddMinutes(-50));
        refTest.Complete(1, 1, 2, 50, ["a1"], ["q2"], ["a2"], now: Now.AddMinutes(-30));
        return refTest;
    }

    [Fact]
    public async Task InvitationsRotateTheTokenAndSavePerRefTest()
    {
        var first = Pending();
        var second = Pending("grace@example.org");
        var tokenBefore = first.Token;
        var unitOfWork = new FakeUnitOfWork(first, second);

        var outcome = await RefTestEmailHandler.SendInvitationsAsync([first.Id, second.Id], unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Failures);
        Assert.Equal([first, second], outcome.Sent);
        Assert.NotEqual(tokenBefore, first.Token);
        Assert.Equal([$"invitation:{first.Id}", "save", $"invitation:{second.Id}", "save"], unitOfWork.Log);
    }

    [Fact]
    public async Task InvitationsAreRefusedForNonPendingAnonymizedAndUnknownRefTests()
    {
        var completed = Completed();
        var anonymized = Pending();
        anonymized.Anonymize(Now);
        var unknown = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork(completed, anonymized);

        var outcome = await RefTestEmailHandler.SendInvitationsAsync(
            [completed.Id, anonymized.Id, unknown], unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Sent);
        Assert.Collection(outcome.Failures,
            f => Assert.Equal("ada@example.org", f.Participant?.Email),
            f => Assert.IsType<InvalidRefTestStatusException>(f.Exception),
            f =>
            {
                Assert.Null(f.Participant);
                Assert.IsType<RefTestNotFoundException>(f.Exception);
            });
        Assert.DoesNotContain("save", unitOfWork.Log);
    }

    [Fact]
    public async Task AFailedSaveRollsBackThatRefTestAndTheBatchContinues()
    {
        var failing = Pending();
        var fine = Pending("grace@example.org");
        var unitOfWork = new FakeUnitOfWork(failing, fine) { FailSaveFor = failing.Id };

        var outcome = await RefTestEmailHandler.SendInvitationsAsync([failing.Id, fine.Id], unitOfWork, CancellationToken.None);

        Assert.Equal(fine, Assert.Single(outcome.Sent));
        Assert.Equal(failing.Id, Assert.Single(outcome.Failures).RefTestId);
        Assert.Contains($"discard:{failing.Id}", unitOfWork.Log);
    }

    [Fact]
    public async Task ResultsAreSentOnlyForCompletedRefTests()
    {
        var completed = Completed();
        var pending = Pending("grace@example.org");
        var unitOfWork = new FakeUnitOfWork(completed, pending);

        var outcome = await RefTestEmailHandler.SendResultsAsync([completed.Id, pending.Id], unitOfWork, CancellationToken.None);

        Assert.Equal(completed, Assert.Single(outcome.Sent));
        Assert.IsType<InvalidRefTestStatusException>(Assert.Single(outcome.Failures).Exception);
        Assert.Equal([$"result:{completed.Id}:1:2", "save", $"discard:{pending.Id}"], unitOfWork.Log);
    }

    private sealed class FakeUnitOfWork(params RefTest[] stored) : IRefTestUnitOfWork
    {
        private RefTest? _pending;
        public List<string> Log { get; } = [];
        public Guid? FailSaveFor { get; init; }

        public Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefTest>>([.. stored.Where(refTest => ids.Contains(refTest.Id))]);

        public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            _pending = refTest;
            Log.Add($"invitation:{refTest.Id}");
            return Task.CompletedTask;
        }

        public Task StageResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            Log.Add($"result:{payload.RefTestId}:{payload.QuestionScore}:{payload.TotalQuestions}");
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailSaveFor is not null && _pending?.Id == FailSaveFor)
                throw new InvalidOperationException("concurrent update");
            Log.Add("save");
            return Task.CompletedTask;
        }

        public Task<RefTest?> DiscardChangesAsync(RefTest refTest, CancellationToken cancellationToken)
        {
            Log.Add($"discard:{refTest.Id}");
            return Task.FromResult<RefTest?>(null);
        }

        public void AddRefTests(IEnumerable<RefTest> refTests) => throw new NotSupportedException();
        public Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelPendingJobsAsync(Guid refTestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ClearPersonalDataExportRequestsAsync(string email, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Action BeginJobStaging() => throw new NotSupportedException();
    }
}
