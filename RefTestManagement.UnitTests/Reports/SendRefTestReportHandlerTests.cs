using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class SendRefTestReportHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 10, 11, 12, DateTimeKind.Utc);

    [Fact]
    public async Task NoRefTestsDoesNotEnqueueAJob()
    {
        var queue = new RecordingQueue();
        var handler = CreateHandler([], ["staff@example.org"], queue);

        var outcome = await handler.HandleAsync([Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(SendRefTestReportFailure.NoRefTests, outcome.Failure);
        Assert.Equal(0, outcome.RefTestCount);
        Assert.Empty(queue.Payloads);
    }

    [Fact]
    public async Task NoRecipientsDoesNotEnqueueAJob()
    {
        var queue = new RecordingQueue();
        var handler = CreateHandler([Snapshot(Guid.NewGuid(), 90)], [], queue);

        var outcome = await handler.HandleAsync([Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(SendRefTestReportFailure.NoRecipients, outcome.Failure);
        Assert.Equal(1, outcome.RefTestCount);
        Assert.Empty(queue.Payloads);
    }

    [Fact]
    public async Task CreatesTheExpectedPayloadAndQueuesIt()
    {
        var refTestId = Guid.NewGuid();
        var queue = new RecordingQueue();
        var handler = CreateHandler(
            [Snapshot(refTestId, 80), Snapshot(Guid.NewGuid(), 79)],
            ["staff@example.org"],
            queue);

        var outcome = await handler.HandleAsync([refTestId], CancellationToken.None);

        Assert.Equal(SendRefTestReportFailure.None, outcome.Failure);
        Assert.Equal(2, outcome.RefTestCount);
        Assert.Equal(1, outcome.RecipientCount);
        var payload = Assert.Single(queue.Payloads);
        Assert.Equal(["staff@example.org"], payload.RecipientEmails);
        Assert.Equal("2026-10-10 10:11:12", payload.Timestamp);
        Assert.Collection(payload.RefTests,
            first =>
            {
                Assert.Equal(refTestId, first.RefTestId);
                Assert.True(first.Passed);
            },
            second => Assert.False(second.Passed));
    }

    [Fact]
    public async Task EnqueueFailureIsReturnedForTheTransportToLog()
    {
        var exception = new InvalidOperationException("queue unavailable");
        var queue = new RecordingQueue { Exception = exception };
        var handler = CreateHandler([Snapshot(Guid.NewGuid(), 90)], ["staff@example.org"], queue);

        var outcome = await handler.HandleAsync([Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(SendRefTestReportFailure.EnqueueFailed, outcome.Failure);
        Assert.Same(exception, outcome.Exception);
        Assert.Equal(1, outcome.RefTestCount);
    }

    [Fact]
    public async Task RequestedCancellationIsNotConvertedToAnEnqueueFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var queue = new RecordingQueue { Exception = new OperationCanceledException() };
        var handler = CreateHandler([Snapshot(Guid.NewGuid(), 90)], ["staff@example.org"], queue);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync([Guid.NewGuid()], cancellation.Token));
    }

    private static SendRefTestReportHandler CreateHandler(
        IReadOnlyList<RefTestReportSnapshot> snapshots,
        string[] recipients,
        RecordingQueue queue) =>
        new(
            new StaticDataSource(snapshots),
            queue,
            new ReportConfiguration { RecipientEmails = recipients },
            new ScoreConfiguration { PassingPercentage = 80 },
            new FixedTimeProvider(Now));

    private static RefTestReportSnapshot Snapshot(Guid id, double percentage) =>
        new(
            id,
            "Season 2026",
            "Ada",
            "Lovelace",
            Now.AddMinutes(-10),
            Now,
            8,
            10,
            9,
            10,
            percentage,
            "en",
            TimeSpan.FromMinutes(10));

    private sealed class StaticDataSource(IReadOnlyList<RefTestReportSnapshot> snapshots)
        : IRefTestReportDataSource
    {
        public Task<IReadOnlyList<RefTestReportSnapshot>> GetAsync(
            IReadOnlyCollection<Guid> refTestIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshots);
    }

    private sealed class RecordingQueue : IReportEmailJobQueue
    {
        public List<ReportEmailPayload> Payloads { get; } = [];
        public Exception? Exception { get; init; }

        public Task EnqueueAsync(ReportEmailPayload payload, CancellationToken cancellationToken)
        {
            if (Exception is not null)
                return Task.FromException(Exception);

            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
