using System.Reflection;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>Single-RefTest update use cases against an in-memory unit of work.</summary>
public sealed class RefTestUpdateHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefTest Pending(bool invitationSent = false, bool sendInvitations = false)
    {
        var refTest = RefTest.Create(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.org", 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: sendInvitations, sendResultsAutomatically: false, now: Now.AddHours(-1));
        if (invitationSent)
            refTest.SendInvitation(Now.AddHours(-1));
        return refTest;
    }

    private static RefTest Completed()
    {
        var refTest = Pending(invitationSent: true);
        refTest.AcceptPrivacyNotice("v1", Now.AddMinutes(-50));
        refTest.Start("v1", Now.AddMinutes(-50));
        refTest.Complete(1, 1, 2, 50, ["a1"], ["q2"], ["a2"], now: Now.AddMinutes(-30));
        return refTest;
    }

    [Fact]
    public async Task ChangingTheEmailClearsOldExportsAndReinvitesWhenAsked()
    {
        var refTest = Pending(invitationSent: true);
        var unitOfWork = new FakeUnitOfWork(refTest);

        await RefTestUpdateHandler.UpdateDetailsAsync(refTest.Id, "Ada", "Byron", "new@example.org",
            resendInvitation: true, unitOfWork, CancellationToken.None);

        Assert.Equal("new@example.org", refTest.Email);
        Assert.Equal(["clear-exports:ada@example.org", $"invitation:{refTest.Id}", "save"], unitOfWork.Log);
    }

    [Fact]
    public async Task KeepingTheEmailNeitherClearsExportsNorReinvites()
    {
        var refTest = Pending(invitationSent: true);
        var unitOfWork = new FakeUnitOfWork(refTest);

        await RefTestUpdateHandler.UpdateDetailsAsync(refTest.Id, "Ada", "Byron", "ada@example.org",
            resendInvitation: true, unitOfWork, CancellationToken.None);

        Assert.Equal(["save"], unitOfWork.Log);
    }

    [Fact]
    public async Task AMissingRefTestThrowsNotFoundWithoutSaving()
    {
        var unitOfWork = new FakeUnitOfWork();

        await Assert.ThrowsAsync<RefTestNotFoundException>(() =>
            RefTestUpdateHandler.RegenerateTokenAsync(Guid.NewGuid(), unitOfWork, CancellationToken.None));
        Assert.Empty(unitOfWork.Log);
    }

    [Theory]
    [InlineData(true, new[] { "n1", "n2" }, "by-number")]
    [InlineData(false, null, "keep")]
    [InlineData(true, null, "random")]
    public async Task ConfigurationPicksQuestionsBySpecificNumbersThenRandomThenKeepsThem(
        bool random, string[]? numbers, string expected)
    {
        var refTest = Pending();
        var titleId = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork(refTest);

        await RefTestUpdateHandler.UpdateConfigurationAsync(refTest.Id, titleId, 2, 45, numbers, random,
            new FakeQuestions(), unitOfWork, CancellationToken.None);

        string[] expectedIds = expected switch
        {
            "by-number" => ["id-n1", "id-n2"],
            "random" => ["r1", "r2"],
            _ => ["q1", "q2"]
        };
        Assert.Equal(expectedIds, refTest.QuestionIds);
        Assert.Equal(titleId, refTest.TitleId);
        Assert.Equal(45, refTest.MaxTimeInMinutes);
        Assert.Equal(["save"], unitOfWork.Log);
    }

    [Fact]
    public async Task ExtendingTimeSavesThenPublishesAtTheProvidedTime()
    {
        var refTest = Pending();
        refTest.AcceptPrivacyNotice("v1", Now);
        refTest.Start("v1", Now);
        var unitOfWork = new FakeUnitOfWork(refTest);
        var (subscriptions, recorder) = EventRecorder.Create();

        await RefTestUpdateHandler.ExtendTimeAsync(refTest.Id, 15, subscriptions, new FixedTime(Now),
            unitOfWork, CancellationToken.None);

        Assert.Equal(45, refTest.MaxTimeInMinutes);
        Assert.Equal(["save"], unitOfWork.Log);
        Assert.Equal([refTest.Id, 45, 15, Now, CancellationToken.None], recorder.Arguments);
    }

    [Fact]
    public async Task EnablingAutomaticInvitationsSendsTheMissingInvitation()
    {
        var refTest = Pending();
        var unitOfWork = new FakeUnitOfWork(refTest);

        await RefTestUpdateHandler.UpdateNotificationSettingsAsync(refTest.Id, true, null, unitOfWork, CancellationToken.None);

        Assert.True(refTest.SendInvitationsAutomatically);
        Assert.Equal([$"invitation:{refTest.Id}", "save"], unitOfWork.Log);
    }

    [Fact]
    public async Task EnablingAutomaticResultsSendsTheMissingResults()
    {
        var refTest = Completed();
        var unitOfWork = new FakeUnitOfWork(refTest);

        await RefTestUpdateHandler.UpdateNotificationSettingsAsync(refTest.Id, null, true, unitOfWork, CancellationToken.None);

        Assert.Equal([$"result:{refTest.Id}:a1:q2:a2", "save"], unitOfWork.Log);
    }

    [Fact]
    public async Task RegeneratingTheTokenReinvitesOnlyWhenAnInvitationWasSent()
    {
        var invited = Pending(invitationSent: true);
        var notInvited = Pending();
        var unitOfWork = new FakeUnitOfWork(invited, notInvited);

        await RefTestUpdateHandler.RegenerateTokenAsync(invited.Id, unitOfWork, CancellationToken.None);
        await RefTestUpdateHandler.RegenerateTokenAsync(notInvited.Id, unitOfWork, CancellationToken.None);

        Assert.Equal([$"invitation:{invited.Id}", "save", "save"], unitOfWork.Log);
    }

    private sealed class FakeUnitOfWork(params RefTest[] stored) : IRefTestUnitOfWork
    {
        public List<string> Log { get; } = [];

        public Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefTest>>([.. stored.Where(refTest => ids.Contains(refTest.Id))]);

        public Task ClearPersonalDataExportRequestsAsync(string email, CancellationToken cancellationToken)
        {
            Log.Add($"clear-exports:{email}");
            return Task.CompletedTask;
        }

        public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            Log.Add($"invitation:{refTest.Id}");
            return Task.CompletedTask;
        }

        public Task StageResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter, CancellationToken cancellationToken)
        {
            Log.Add($"result:{payload.RefTestId}:{string.Join(',', payload.SelectedAnswerIds)}:" +
                    $"{string.Join(',', payload.WrongQuestionIds)}:{string.Join(',', payload.WrongAnswerIds)}");
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

        public Task<RefTest?> DiscardChangesAsync(RefTest refTest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CancelPendingJobsAsync(Guid refTestId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Action BeginJobStaging() => throw new NotSupportedException();
    }

    private sealed class FakeQuestions : IIhfRulesQuestionsService
    {
        public Task<List<string>> GetRandomQuestionIdsAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<List<string>>([.. Enumerable.Range(1, count).Select(i => $"r{i}")]);

        public Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default) =>
            Task.FromResult<List<string>>([.. numbers.Select(n => $"id-{n}")]);

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

    public class EventRecorder : DispatchProxy
    {
        public object?[] Arguments { get; private set; } = [];

        public static (IRefTestSubscriptionService Service, EventRecorder Recorder) Create()
        {
            var service = DispatchProxy.Create<IRefTestSubscriptionService, EventRecorder>();
            return (service, (EventRecorder)(object)service);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRefTestSubscriptionService.PublishTimeExtendedAsync))
                throw new NotSupportedException(targetMethod?.Name);
            Arguments = args ?? [];
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
