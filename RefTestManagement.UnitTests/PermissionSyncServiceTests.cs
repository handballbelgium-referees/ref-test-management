using System.Net;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Auth0.Configurations;
using Handball.Belgium.RefTestManagement.Auth0.Models;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PermissionSyncServiceTests
{
    [Fact]
    public async Task TransientFailureRetriesWithTimeProviderAndRecovers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var managementService = new FakeAuth0ManagementService((attempt, _) =>
            attempt == 1
                ? Task.FromException(new HttpRequestException(
                    "sensitive Auth0 response",
                    null,
                    HttpStatusCode.ServiceUnavailable))
                : Task.CompletedTask);
        var logger = new CapturingLogger();
        using var service = CreateService(managementService, timeProvider, logger);

        await service.StartAsync(cancellationToken);
        await managementService.WaitForCallCountAsync(1).WaitAsync(cancellationToken);
        await timeProvider.WaitForTimerCountAsync(1).WaitAsync(cancellationToken);

        Assert.Equal(1, managementService.CallCount);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, timeProvider.ScheduledDelays);

        timeProvider.AdvanceBy(TimeSpan.FromSeconds(2));
        await managementService.WaitForCallCountAsync(2).WaitAsync(cancellationToken);
        await service.ExecuteTask!.WaitAsync(cancellationToken);

        Assert.Equal(2, managementService.CallCount);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.Message.Contains("completed successfully", StringComparison.Ordinal));
        Assert.DoesNotContain(
            "sensitive Auth0 response",
            string.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message)));
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    [Theory]
    [InlineData("", "unit-test-secret")]
    [InlineData("unit-test-client", "")]
    public async Task MissingManagementCredentialsSkipSyncWithoutRetry(
        string managementClientId,
        string managementClientSecret)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var managementService = new FakeAuth0ManagementService((_, _) => Task.CompletedTask);
        var logger = new CapturingLogger();
        var configuration = ConfiguredManagementApi();
        configuration.ManagementClientId = managementClientId;
        configuration.ManagementClientSecret = managementClientSecret;
        using var service = CreateService(managementService, timeProvider, logger, configuration);

        await service.StartAsync(cancellationToken);
        await service.ExecuteTask!.WaitAsync(cancellationToken);

        Assert.Equal(0, managementService.CallCount);
        Assert.Empty(timeProvider.ScheduledDelays);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("required configuration is missing", warning.Message);
        Assert.DoesNotContain("unit-test-client", warning.Message);
        Assert.DoesNotContain("unit-test-secret", warning.Message);
    }

    [Fact]
    public async Task PersistentTransientFailureStopsAfterFiveAttemptsAndLogsSanitizedWarning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var retryDelays = new[]
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(16)
        };
        var managementService = new FakeAuth0ManagementService((_, _) =>
            Task.FromException(new HttpRequestException(
                "sensitive Auth0 response",
                null,
                HttpStatusCode.ServiceUnavailable)));
        var logger = new CapturingLogger();
        using var service = CreateService(managementService, timeProvider, logger);

        await service.StartAsync(cancellationToken);
        for (var attempt = 1; attempt <= retryDelays.Length; attempt++)
        {
            await managementService.WaitForCallCountAsync(attempt).WaitAsync(cancellationToken);
            await timeProvider.WaitForTimerCountAsync(attempt).WaitAsync(cancellationToken);

            Assert.Equal(attempt, managementService.CallCount);
            timeProvider.AdvanceBy(retryDelays[attempt - 1]);
        }

        await managementService.WaitForCallCountAsync(retryDelays.Length + 1)
            .WaitAsync(cancellationToken);
        await service.ExecuteTask!.WaitAsync(cancellationToken);

        Assert.Equal(5, managementService.CallCount);
        Assert.Equal(retryDelays, timeProvider.ScheduledDelays);
        var finalWarning = logger.Entries.Last(entry => entry.Level == LogLevel.Warning);
        Assert.Contains("5 attempt(s)", finalWarning.Message);
        Assert.DoesNotContain(
            "sensitive Auth0 response",
            string.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message)));
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    [Fact]
    public async Task StoppingCancelsPendingRetryDelayPromptly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var managementService = new FakeAuth0ManagementService((_, _) =>
            Task.FromException(new HttpRequestException(
                "sensitive Auth0 response",
                null,
                HttpStatusCode.ServiceUnavailable)));
        var logger = new CapturingLogger();
        using var service = CreateService(managementService, timeProvider, logger);

        await service.StartAsync(cancellationToken);
        await managementService.WaitForCallCountAsync(1).WaitAsync(cancellationToken);
        await timeProvider.WaitForTimerCountAsync(1).WaitAsync(cancellationToken);

        await service.StopAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(1, managementService.CallCount);
        Assert.Single(timeProvider.ScheduledDelays);
        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Message.Contains("failed after", StringComparison.Ordinal));
    }

    private static PermissionSyncService CreateService(
        FakeAuth0ManagementService managementService,
        ManualTimeProvider timeProvider,
        CapturingLogger logger,
        Auth0ManagementConfiguration? configuration = null) =>
        new(
            managementService,
            Options.Create(configuration ?? ConfiguredManagementApi()),
            timeProvider,
            logger);

    private static Auth0ManagementConfiguration ConfiguredManagementApi() => new()
    {
        Domain = "unit-test.auth0.local",
        Audience = "unit-test-api",
        ManagementClientId = "unit-test-client",
        ManagementClientSecret = "unit-test-secret"
    };

    private sealed class FakeAuth0ManagementService(
        Func<int, CancellationToken, Task> syncPermissions)
        : IAuth0ManagementService
    {
        private readonly object _gate = new();
        private TaskCompletionSource<bool> _callCountChanged = NewSignal();
        private int _callCount;

        public int CallCount
        {
            get
            {
                lock (_gate)
                    return _callCount;
            }
        }

        public async Task WaitForCallCountAsync(int expectedCount)
        {
            while (true)
            {
                Task wait;
                lock (_gate)
                {
                    if (_callCount >= expectedCount)
                        return;
                    wait = _callCountChanged.Task;
                }

                await wait.ConfigureAwait(false);
            }
        }

        public Task SyncPermissionsAsync(
            IReadOnlyList<string> permissions,
            CancellationToken cancellationToken = default)
        {
            int attempt;
            TaskCompletionSource<bool> changed;
            lock (_gate)
            {
                attempt = ++_callCount;
                changed = _callCountChanged;
                _callCountChanged = NewSignal();
            }

            changed.TrySetResult(true);
            return syncPermissions(attempt, cancellationToken);
        }

        public Task<IReadOnlyList<Auth0User>> GetUsersWithPermissionAsync(
            string permission,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<string>> GetUserPermissionsAsync(
            string userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = [];
        private readonly List<TimeSpan> _scheduledDelays = [];
        private DateTimeOffset _utcNow = utcNow;
        private TaskCompletionSource<bool> _timerCountChanged = NewSignal();
        private int _createdTimerCount;

        public IReadOnlyList<TimeSpan> ScheduledDelays
        {
            get
            {
                lock (_gate)
                    return _scheduledDelays.ToArray();
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
                return _utcNow;
        }

        public async Task WaitForTimerCountAsync(int expectedCount)
        {
            while (true)
            {
                Task wait;
                lock (_gate)
                {
                    if (_createdTimerCount >= expectedCount)
                        return;
                    wait = _timerCountChanged.Task;
                }

                await wait.ConfigureAwait(false);
            }
        }

        public void AdvanceBy(TimeSpan amount)
        {
            ManualTimer[] timers;
            DateTimeOffset now;
            lock (_gate)
            {
                _utcNow += amount;
                now = _utcNow;
                timers = [.._timers];
            }

            foreach (var timer in timers)
                timer.FireIfDue(now);
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            TaskCompletionSource<bool> changed;
            ManualTimer timer;
            lock (_gate)
            {
                timer = new ManualTimer(this, callback, state, dueTime, period);
                _timers.Add(timer);
                _scheduledDelays.Add(dueTime);
                _createdTimerCount++;
                changed = _timerCountChanged;
                _timerCountChanged = NewSignal();
            }

            changed.TrySetResult(true);
            return timer;
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _timeProvider;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private DateTimeOffset? _dueAt;
            private TimeSpan _period;
            private bool _disposed;

            public ManualTimer(
                ManualTimeProvider timeProvider,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _timeProvider = timeProvider;
                _callback = callback;
                _state = state;
                _period = period;
                _dueAt = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : timeProvider._utcNow + dueTime;
            }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (_timeProvider._gate)
                {
                    if (_disposed)
                        return false;

                    _period = period;
                    _dueAt = dueTime == Timeout.InfiniteTimeSpan
                        ? null
                        : _timeProvider._utcNow + dueTime;
                    return true;
                }
            }

            public void Dispose()
            {
                lock (_timeProvider._gate)
                {
                    _disposed = true;
                    _dueAt = null;
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                object? state;
                lock (_timeProvider._gate)
                {
                    if (_disposed || _dueAt is not { } dueAt || dueAt > now)
                        return;

                    state = _state;
                    _dueAt = _period <= TimeSpan.Zero
                        ? null
                        : dueAt + _period;
                }

                _callback(state);
            }
        }
    }

    private sealed class CapturingLogger : ILogger<PermissionSyncService>
    {
        public List<CapturedLog> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new CapturedLog(logLevel, formatter(state, exception), exception));
    }

    private sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception);

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
