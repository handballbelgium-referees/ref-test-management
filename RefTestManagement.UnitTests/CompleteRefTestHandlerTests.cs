using System.Reflection;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>RefTest completion against an in-memory unit of work.</summary>
public sealed class CompleteRefTestHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefTest InProgress(bool sendResults, DateTime startedAt)
    {
        var refTest = RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.org", 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: sendResults, now: startedAt);
        refTest.AcceptPrivacyNotice("v1", startedAt);
        refTest.Start("v1", startedAt);
        return refTest;
    }

    private static (CompleteRefTestHandler, Recorder) Handler(int delayMinutes = 0)
    {
        var (subscriptions, recorder) = Recorder.Create();
        return (new CompleteRefTestHandler(new FakeScoring(), subscriptions,
            new EmailConfiguration { ScheduledDelayMinutes = delayMinutes }, new FixedTime(Now)), recorder);
    }

    [Fact]
    public async Task CompletionScoresStagesTheDelayedResultEmailSavesOnceThenPublishes()
    {
        var refTest = InProgress(sendResults: true, Now.AddMinutes(-10));
        var unitOfWork = new FakeUnitOfWork();
        var (handler, recorder) = Handler(delayMinutes: 15);

        await handler.HandleAsync(refTest, ["a1"], "nl", RefTestCompletionSource.Participant, unitOfWork, CancellationToken.None);

        Assert.Equal(RefTestStatus.Completed, refTest.Status);
        Assert.Equal(Now, refTest.CompletedAt);
        Assert.Equal(["q2"], refTest.WrongQuestionIds);
        Assert.Equal([$"result:{refTest.Id}:{Now.AddMinutes(15):O}", "save"], unitOfWork.Log);
        Assert.Equal(1, recorder.Count);
    }

    [Fact]
    public async Task NoResultEmailWhenResultsAreNotSentAutomatically()
    {
        var refTest = InProgress(sendResults: false, Now.AddMinutes(-10));
        var unitOfWork = new FakeUnitOfWork();
        var (handler, _) = Handler();

        await handler.HandleAsync(refTest, ["a1"], null, RefTestCompletionSource.Participant, unitOfWork, CancellationToken.None);

        Assert.Equal(["save"], unitOfWork.Log);
    }

    [Fact]
    public async Task OnlyTheExpirationServiceMayCompleteAfterTheDeadline()
    {
        var (handler, _) = Handler();
        var participant = InProgress(sendResults: false, Now.AddHours(-2));
        var expired = InProgress(sendResults: false, Now.AddHours(-2));

        await Assert.ThrowsAsync<InvalidRefTestStatusException>(() => handler.HandleAsync(
            participant, [], null, RefTestCompletionSource.Participant, new FakeUnitOfWork(), CancellationToken.None));
        await handler.HandleAsync(
            expired, [], null, RefTestCompletionSource.ExpirationService, new FakeUnitOfWork(), CancellationToken.None);

        Assert.Equal(RefTestStatus.InProgress, participant.Status);
        Assert.Equal(RefTestStatus.Completed, expired.Status);
    }

    [Fact]
    public async Task ARefTestThatIsNotInProgressIsRejectedBeforeScoring()
    {
        var pending = RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.org", 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: false);
        var unitOfWork = new FakeUnitOfWork();
        var (handler, _) = Handler();

        await Assert.ThrowsAsync<InvalidRefTestStatusException>(() => handler.HandleAsync(
            pending, [], null, RefTestCompletionSource.Participant, unitOfWork, CancellationToken.None));
        Assert.Empty(unitOfWork.Log);
    }

    private sealed class FakeScoring : IIhfRulesQuestionsService
    {
        public Task<ScoreCalculation> CalculateScoreAsync(IReadOnlyList<string> questionIds,
            IReadOnlyList<string> selectedAnswerIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScoreCalculation(1, 1, 2, 2, 50, ["q2"], ["a2"]));

        public Task<List<string>> GetRandomQuestionIdsAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Question>> GetQuestionsByIdAsync(IReadOnlyList<string> ids, bool includeNumber = false,
            bool includeIsCorrect = false, bool randomAnswerOrder = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Question>> SearchQuestionsByNumberAsync(string? number, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Question>> GetQuestionsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IRefTestUnitOfWork
    {
        public List<string> Log { get; } = [];

        public Task StageResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            Log.Add($"result:{payload.RefTestId}:{executeAfter:O}");
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Log.Add("save");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void AddRefTests(IEnumerable<RefTest> refTests) => throw new NotSupportedException();
        public Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RefTest?> DiscardChangesAsync(RefTest refTest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelPendingJobsAsync(Guid refTestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ClearPersonalDataExportRequestsAsync(string email, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Action BeginJobStaging() => throw new NotSupportedException();
    }

    public class Recorder : DispatchProxy
    {
        public int Count { get; private set; }

        public static (IRefTestSubscriptionService Service, Recorder Recorder) Create()
        {
            var service = DispatchProxy.Create<IRefTestSubscriptionService, Recorder>();
            return (service, (Recorder)(object)service);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRefTestSubscriptionService.PublishRefTestCompletedAsync))
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
