using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestSubscriptionStreamTests
{
    private static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan LongInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task DeniedUpFrontYieldsNothingAndCompletes()
    {
        var verifier = new ScriptedVerifier(_ => false);

        var received = await ConsumeAsync(new FakeSource(), verifier, FastInterval, TimeSpan.Zero);

        Assert.Empty(received);
        Assert.Equal(1, verifier.Calls);
    }

    [Fact]
    public async Task RevocationWhileIdleEndsStreamWithinOneInterval()
    {
        // Initial check allowed; every timer re-check after that is denied. No events are sent.
        var verifier = new ScriptedVerifier(call => call == 0);

        var received = await ConsumeAsync(new FakeSource(), verifier, FastInterval, TimeSpan.Zero);

        Assert.Empty(received);
        Assert.True(verifier.Calls >= 2);
    }

    [Fact]
    public async Task EventsBeforeRevocationAreDeliveredAndNoneAfter()
    {
        // Per-event throttle zero: each event is checked live. Allowed for the first three checks only.
        var verifier = new ScriptedVerifier(call => call < 3);
        var source = new FakeSource();

        var received = await ConsumeAsync(
            source,
            verifier,
            LongInterval,
            TimeSpan.Zero,
            feed: async s =>
            {
                for (var i = 1; i <= 4; i++)
                    await s.Pipe.Writer.WriteAsync(Message(i));
            });

        Assert.Equal([1, 2], received);
    }

    [Fact]
    public async Task EventChecksAreThrottledWhileLastCheckIsRecent()
    {
        var verifier = new ScriptedVerifier(_ => true);
        var source = new FakeSource();

        var received = await ConsumeAsync(
            source,
            verifier,
            LongInterval,
            TimeSpan.FromHours(1),
            take: 5,
            feed: async s =>
            {
                for (var i = 1; i <= 5; i++)
                    await s.Pipe.Writer.WriteAsync(Message(i));
            });

        Assert.Equal([1, 2, 3, 4, 5], received);
        Assert.Equal(1, verifier.Calls);
    }

    [Fact]
    public async Task ProviderFailureDuringEventCheckFailsClosed()
    {
        // The live verifier returns false on timeout or provider error, so the stream must end.
        var verifier = new ScriptedVerifier(call => call == 0);
        var source = new FakeSource();

        var received = await ConsumeAsync(
            source,
            verifier,
            LongInterval,
            TimeSpan.Zero,
            feed: async s => await s.Pipe.Writer.WriteAsync(Message(1)));

        Assert.Empty(received);
    }

    [Fact]
    public async Task TeardownFaultFromPendingReadDoesNotReplaceConsumerStop()
    {
        // The consumer stops after one message while a read is pending. Disposing the source
        // faults that read. The fault must be swallowed, not thrown out of the consumer's loop.
        var verifier = new ScriptedVerifier(_ => true);
        var source = new FakeSource { DisposeFault = new InvalidOperationException("dispose failed") };

        var received = await ConsumeAsync(
            source,
            verifier,
            LongInterval,
            TimeSpan.Zero,
            take: 1,
            feed: s =>
            {
                s.Pipe.Writer.TryWrite(Message(1));
                return Task.CompletedTask;
            });

        Assert.Equal([1], received);
    }

    private static RefTestSubscriptionMessage Message(int index) =>
        new("Test", JsonSerializer.SerializeToElement(new { Index = index }));

    private static async Task<List<int>> ConsumeAsync(
        FakeSource source,
        ScriptedVerifier verifier,
        TimeSpan recheckInterval,
        TimeSpan eventRecheckThrottle,
        int? take = null,
        Func<FakeSource, Task>? feed = null)
    {
        using var guard = new CancellationTokenSource(Guard);
        var stream = RefTestSubscriptions.StreamWhilePermitted(
            "topic",
            "permission",
            new ClaimsPrincipal(new ClaimsIdentity()),
            new FakeReceiver(source),
            verifier,
            TimeProvider.System,
            recheckInterval,
            eventRecheckThrottle,
            guard.Token);

        var feeding = feed?.Invoke(source) ?? Task.CompletedTask;
        var received = new List<int>();
        await foreach (var message in stream)
        {
            received.Add(message.Payload.GetProperty("Index").GetInt32());
            if (take is not null && received.Count >= take)
                break;
        }

        await feeding;
        return received;
    }

    private sealed class ScriptedVerifier(Func<int, bool> decide) : ILivePermissionVerifier
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string requiredPermission,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(decide(Interlocked.Increment(ref _calls) - 1));
    }

    private sealed class FakeSource : ISourceStream<RefTestSubscriptionMessage>
    {
        public Channel<RefTestSubscriptionMessage> Pipe { get; } =
            Channel.CreateUnbounded<RefTestSubscriptionMessage>();

        /// <summary>When set, disposing the source faults any pending read with this exception.</summary>
        public Exception? DisposeFault { get; init; }

        public async IAsyncEnumerable<RefTestSubscriptionMessage> ReadEventsAsync()
        {
            await foreach (var message in Pipe.Reader.ReadAllAsync())
                yield return message;
        }

        async IAsyncEnumerable<object?> ISourceStream.ReadEventsAsync()
        {
            await foreach (var message in Pipe.Reader.ReadAllAsync())
                yield return message;
        }

        public ValueTask DisposeAsync()
        {
            Pipe.Writer.TryComplete(DisposeFault);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeReceiver(FakeSource source) : ITopicEventReceiver
    {
        public ValueTask<ISourceStream<TMessage>> SubscribeAsync<TMessage>(
            string topic,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult((ISourceStream<TMessage>)(object)source);

        public ValueTask<ISourceStream<TMessage>> SubscribeAsync<TMessage>(
            string topic,
            int? capacity,
            TopicBufferFullMode? bufferFullMode,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult((ISourceStream<TMessage>)(object)source);
    }
}
