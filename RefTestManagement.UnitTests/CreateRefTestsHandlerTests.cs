using System.Reflection;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// The creation use case without EF Core: an in-memory unit of work records what is staged and
/// saved, so the transaction rules can be checked directly.
/// </summary>
public sealed class CreateRefTestsHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static CreateRefTestsCommand Command(
        bool requiresApproval = false,
        bool sendInvitations = true,
        params RefTestParticipant[] participants) =>
        new(
            participants.Length > 0 ? participants : [Ada, Grace],
            Guid.NewGuid(), "Season 2026",
            NumberOfQuestions: 2, MaxTimeInMinutes: 30,
            RandomQuestionsForEachUser: false, SpecificQuestionNumbers: null,
            SendAutomatedInvitations: sendInvitations, SendAutomatedResults: false,
            ScheduledAt: null, RequiresApproval: requiresApproval,
            CreatorName: "Creator", CreatorEmail: "creator@example.org");

    private static readonly RefTestParticipant Ada = new("Ada", "Lovelace", "ada@example.org");
    private static readonly RefTestParticipant Grace = new("Grace", "Hopper", "grace@example.org");

    private static CreateRefTestsHandler Handler(IRefTestSubscriptionService? subscriptions = null) =>
        new(new FakeQuestions(), subscriptions ?? SubscriptionCounter.Create().Service, new FixedTime(Now));

    [Fact]
    public async Task InvitationsAreStagedAndEverythingIsSavedOnce()
    {
        var unitOfWork = new FakeUnitOfWork();
        var (subscriptions, counter) = SubscriptionCounter.Create();

        var outcome = await Handler(subscriptions).HandleAsync(Command(), unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Failures);
        Assert.Equal(["ada@example.org", "grace@example.org"], outcome.Created.Select(r => r.Email));
        Assert.Equal(outcome.Created, unitOfWork.Added);
        Assert.Equal(2, unitOfWork.Jobs.Count(job => job.StartsWith("invitation:")));
        Assert.Equal(1, unitOfWork.Saves);
        Assert.Equal(2, counter.Created);
        Assert.All(outcome.Created, refTest => Assert.Equal(Now, refTest.CreatedAt));
    }

    [Fact]
    public async Task ApprovalFlowStagesOneNotificationAndNoInvitations()
    {
        var unitOfWork = new FakeUnitOfWork();

        var outcome = await Handler().HandleAsync(Command(requiresApproval: true), unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Failures);
        Assert.Equal(["approval:2"], unitOfWork.Jobs);
        Assert.All(outcome.Created, refTest => Assert.Equal(RefTestStatus.PendingApproval, refTest.Status));
        Assert.Equal(1, unitOfWork.Saves);
    }

    [Fact]
    public async Task AFailedInvitationDropsOnlyThatRefTestAndItsStagedJob()
    {
        var unitOfWork = new FakeUnitOfWork { FailInvitationFor = "grace@example.org" };

        var outcome = await Handler().HandleAsync(Command(), unitOfWork, CancellationToken.None);

        var created = Assert.Single(outcome.Created);
        Assert.Equal("ada@example.org", created.Email);
        var failure = Assert.Single(outcome.Failures);
        Assert.Equal(CreateRefTestsFailureStage.Invitation, failure.Stage);
        Assert.Equal(Grace, failure.Participant);
        Assert.NotNull(failure.RefTestId);
        Assert.Equal([$"invitation:{created.Id}"], unitOfWork.Jobs);
        Assert.Equal(1, unitOfWork.Saves);
    }

    [Fact]
    public async Task AFailedApprovalNotificationSavesNothing()
    {
        var unitOfWork = new FakeUnitOfWork { FailApproval = true };
        var (subscriptions, counter) = SubscriptionCounter.Create();

        var outcome = await Handler(subscriptions).HandleAsync(Command(requiresApproval: true), unitOfWork, CancellationToken.None);

        Assert.Empty(outcome.Created);
        Assert.Equal(2, outcome.Failures.Count);
        Assert.All(outcome.Failures, f => Assert.Equal(CreateRefTestsFailureStage.ApprovalNotification, f.Stage));
        Assert.Empty(unitOfWork.Jobs);
        Assert.Empty(unitOfWork.Added);
        Assert.Equal(0, unitOfWork.Saves);
        Assert.Equal(0, counter.Created);
    }

    [Fact]
    public async Task AnInvalidParticipantIsReportedAndTheRestAreCreated()
    {
        var unitOfWork = new FakeUnitOfWork();
        var invalid = new RefTestParticipant("", "Nobody", "nobody@example.org");

        var outcome = await Handler().HandleAsync(
            Command(sendInvitations: false, participants: [Ada, invalid]), unitOfWork, CancellationToken.None);

        Assert.Equal("ada@example.org", Assert.Single(outcome.Created).Email);
        var failure = Assert.Single(outcome.Failures);
        Assert.Equal(CreateRefTestsFailureStage.Build, failure.Stage);
        Assert.IsType<ArgumentException>(failure.Exception);
        Assert.Empty(unitOfWork.Jobs);
    }

    [Fact]
    public async Task CancellationStopsTheBatchInsteadOfBeingReportedAsAFailure()
    {
        using var cts = new CancellationTokenSource();
        var unitOfWork = new FakeUnitOfWork { CancelOnInvitation = cts };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Handler().HandleAsync(Command(), unitOfWork, cts.Token));
        Assert.Equal(0, unitOfWork.Saves);
    }

    private sealed class FakeUnitOfWork : IRefTestUnitOfWork
    {
        public List<string> Jobs { get; } = [];
        public List<RefTest> Added { get; } = [];
        public int Saves { get; private set; }
        public string? FailInvitationFor { get; init; }
        public bool FailApproval { get; init; }
        public CancellationTokenSource? CancelOnInvitation { get; init; }

        public void AddRefTests(IEnumerable<RefTest> refTests) => Added.AddRange(refTests);

        public Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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

        public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            Jobs.Add($"invitation:{refTest.Id}");
            if (CancelOnInvitation is not null)
            {
                CancelOnInvitation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Fails after staging, as a real enqueue can: the partly staged job must be discarded.
            if (refTest.Email == FailInvitationFor)
                throw new InvalidOperationException("token protection failed");
            return Task.CompletedTask;
        }

        public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken)
        {
            Jobs.Add($"approval:{payload.RefTests.Count}");
            if (FailApproval)
                throw new InvalidOperationException("payload could not be built");
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

    private sealed class FakeQuestions : IIhfRulesQuestionsService
    {
        public Task<List<string>> GetRandomQuestionIdsAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<List<string>>([.. Enumerable.Range(1, count).Select(i => $"q{i}")]);

        public Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<Question>> GetQuestionsByIdAsync(IReadOnlyList<string> ids, bool includeNumber = false,
            bool includeIsCorrect = false, bool randomAnswerOrder = true, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<Question>> SearchQuestionsByNumberAsync(string? number, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<Question>> GetQuestionsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ScoreCalculation> CalculateScoreAsync(IReadOnlyList<string> questionIds,
            IReadOnlyList<string> selectedAnswerIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    public class SubscriptionCounter : DispatchProxy
    {
        public int Created { get; private set; }

        public static (IRefTestSubscriptionService Service, SubscriptionCounter Counter) Create()
        {
            var service = DispatchProxy.Create<IRefTestSubscriptionService, SubscriptionCounter>();
            return (service, (SubscriptionCounter)(object)service);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRefTestSubscriptionService.PublishRefTestCreatedAsync))
                throw new NotSupportedException(targetMethod?.Name);
            Created++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
