using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestSessionTokenTests
{
    private static RefTest NewRefTest(Guid titleId, int maxTimeInMinutes = 30) =>
        RefTest.Create(
            titleId,
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: maxTimeInMinutes,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: true);

    private static RefTestSessionTokenService NewSessionTokenService(TimeProvider? timeProvider = null) =>
        new(new EphemeralDataProtectionProvider(), timeProvider ?? TimeProvider.System);

    private static JobEnqueueService NewJobEnqueueService(RefTestManagementContext context) =>
        new(
            context,
            new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider()),
            NullLogger<JobEnqueueService>.Instance);

    private static async Task<Guid> SeedTitleAsync(SqliteTestDatabase database)
    {
        await using var context = database.CreateContext();
        var title = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return title.Id;
    }

    [Fact]
    public void Create_ProducesProtectedCredentialBoundToTheRefTestAndInvitationHash()
    {
        var refTest = NewRefTest(Guid.NewGuid());
        var service = NewSessionTokenService();

        var token = service.Create(refTest);

        Assert.StartsWith(RefTestSessionTokenService.TokenPrefix, token);
        Assert.NotEqual(refTest.GetIssuedToken(), token);
        Assert.True(service.TryUnprotect(token, out var claims));
        Assert.NotNull(claims);
        Assert.Equal(refTest.Id, claims.RefTestId);
        Assert.Equal(refTest.Token, claims.InvitationTokenHash);
    }

    [Fact]
    public void CredentialValidation_RejectsTamperedAndExpiredCredentials()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var refTest = NewRefTest(Guid.NewGuid());
        var service = NewSessionTokenService(clock);
        var token = service.Create(refTest);
        var tamperedToken = token[..(RefTestSessionTokenService.TokenPrefix.Length + 4)]
                            + (token[RefTestSessionTokenService.TokenPrefix.Length + 4] == 'A' ? 'B' : 'A')
                            + token[(RefTestSessionTokenService.TokenPrefix.Length + 5)..];

        Assert.False(service.TryUnprotect(tamperedToken, out _));
        Assert.True(service.TryUnprotect(token, out var claims));
        Assert.NotNull(claims);
        Assert.True(service.IsValidFor(claims, refTest));

        clock.Advance(TimeSpan.FromHours(12));

        Assert.False(service.IsValidFor(claims, refTest));
    }

    [Fact]
    public void InProgressCredentialTracksAdministrativeTimeExtensions()
    {
        var refTest = RefTest.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 800,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: true);
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        var start = new DateTimeOffset(DateTime.SpecifyKind(refTest.StartedAt!.Value, DateTimeKind.Utc));
        var clock = new ManualTimeProvider(start);
        var service = NewSessionTokenService(clock);
        var token = service.Create(refTest);
        Assert.True(service.TryUnprotect(token, out var claims));
        Assert.NotNull(claims);

        refTest.ExtendTime(60);
        clock.Advance(TimeSpan.FromHours(14) + TimeSpan.FromMinutes(30));
        Assert.True(service.IsValidFor(claims, refTest));

        clock.Advance(TimeSpan.FromMinutes(51));
        Assert.False(service.IsValidFor(claims, refTest));
    }

    [Fact]
    public void PendingCredentialRemainsValidThroughAStartedLongTest()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var refTest = RefTest.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 800,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: true);
        refTest.AcceptPrivacyNotice("v1");
        var service = NewSessionTokenService(clock);
        var token = service.Create(refTest);
        refTest.Start("v1");
        Assert.True(service.TryUnprotect(token, out var claims));
        Assert.NotNull(claims);

        clock.Advance(TimeSpan.FromHours(14) + TimeSpan.FromMinutes(19));
        Assert.True(service.IsValidFor(claims, refTest));

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.False(service.IsValidFor(claims, refTest));
    }

    [Fact]
    public void InProgressCredentialRemainsValidUntilOneHourAfterTheTestDeadline()
    {
        var refTest = RefTest.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 800,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: true);
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        var start = new DateTimeOffset(DateTime.SpecifyKind(refTest.StartedAt!.Value, DateTimeKind.Utc));
        var clock = new ManualTimeProvider(start);
        var service = NewSessionTokenService(clock);
        var token = service.Create(refTest);
        Assert.True(service.TryUnprotect(token, out var claims));
        Assert.NotNull(claims);

        clock.Advance(TimeSpan.FromHours(12));
        Assert.True(service.IsValidFor(claims, refTest));

        clock.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(21));
        Assert.False(service.IsValidFor(claims, refTest));
    }

    [Fact]
    public async Task CreateRefTestSession_RequiresPrivacyNoticeAcceptance()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId);
        var token = refTest.GetIssuedToken();

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var context = database.CreateContext();

        await Assert.ThrowsAsync<RefTestValidationException>(() =>
            RefTestLifecycleMutations.CreateRefTestSessionAsync(
                token,
                context,
                NewSessionTokenService(),
                ct));
    }

    [Fact]
    public async Task SessionCredentialResolvesUntilTheInvitationTokenIsRotated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId);
        var invitationToken = refTest.GetIssuedToken();
        refTest.AcceptPrivacyNotice("v1");

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        var sessionTokenService = NewSessionTokenService();
        await using var context = database.CreateContext();
        var session = await RefTestLifecycleMutations.CreateRefTestSessionAsync(
            invitationToken,
            context,
            sessionTokenService,
            ct);

        var result = await RefTestQueries.GetRefTestByTokenAsync(
            session.SessionToken,
            context,
            new RefTestExpirationConfiguration(),
            sessionTokenService,
            NewJobEnqueueService(context),
            ct);

        Assert.NotNull(result);
        Assert.Equal(RefTestStatus.Pending, result.Status);

        var storedRefTest = await context.RefTests.SingleAsync(candidate => candidate.Id == refTest.Id, ct);
        storedRefTest.RegenerateToken();
        await context.SaveChangesWithRetryAsync(ct);

        var notFound = await Assert.ThrowsAsync<RefTestNotFoundException>(() =>
            RefTestQueries.GetRefTestByTokenAsync(
                session.SessionToken,
                context,
                new RefTestExpirationConfiguration(),
                sessionTokenService,
                NewJobEnqueueService(context),
                ct));

        Assert.Equal("RefTest not found", notFound.Message);
    }

    [Fact]
    public async Task SessionCredentialResolvesAfterDefaultExpiryWhenTheTestStartedInTime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId, maxTimeInMinutes: 800);
        refTest.AcceptPrivacyNotice("v1");
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var sessionTokenService = NewSessionTokenService(clock);
        var sessionToken = sessionTokenService.Create(refTest);

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var context = database.CreateContext();
        var storedRefTest = await context.RefTests.SingleAsync(candidate => candidate.Id == refTest.Id, ct);
        storedRefTest.Start("v1");
        await context.SaveChangesWithRetryAsync(ct);

        clock.Advance(TimeSpan.FromHours(12) + TimeSpan.FromMinutes(1));

        var result = await RefTestQueries.GetRefTestByTokenAsync(
            sessionToken,
            context,
            new RefTestExpirationConfiguration(),
            sessionTokenService,
            NewJobEnqueueService(context),
            ct);

        Assert.NotNull(result);
        Assert.Equal(RefTestStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task SessionLockBlocksAStaleCredentialAfterInvitationTokenRotation()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId);
        refTest.AcceptPrivacyNotice("v1");
        var sessionTokenService = NewSessionTokenService();
        var sessionToken = sessionTokenService.Create(refTest);

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var context = database.CreateContext();
        var storedRefTest = await context.RefTests.SingleAsync(candidate => candidate.Id == refTest.Id, ct);
        storedRefTest.RegenerateToken();
        await context.SaveChangesWithRetryAsync(ct);

        var events = new List<RefTestSessionEvent>();
        await foreach (var sessionEvent in RefTestSubscriptions.SubscribeToRefTestSessionLock(
                           sessionToken,
                           "stale-session",
                           context,
                           new RefTestSessionService(),
                           sessionTokenService,
                           TimeProvider.System,
                           ct))
        {
            events.Add(sessionEvent);
        }

        Assert.Collection(events, sessionEvent =>
            Assert.Equal(RefTestSessionStatus.Blocked, sessionEvent.Status));
    }

    [Fact]
    public async Task SessionLockAcquiresForAValidSessionCredential()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId);
        refTest.AcceptPrivacyNotice("v1");
        var sessionTokenService = NewSessionTokenService();
        var sessionToken = sessionTokenService.Create(refTest);

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var context = database.CreateContext();
        await using var subscription = RefTestSubscriptions.SubscribeToRefTestSessionLock(
            sessionToken,
            "active-session",
            context,
            new RefTestSessionService(),
            sessionTokenService,
            TimeProvider.System,
            ct).GetAsyncEnumerator(ct);

        Assert.True(await subscription.MoveNextAsync());
        Assert.Equal(RefTestSessionStatus.Acquired, subscription.Current.Status);
    }

    [Fact]
    public async Task SessionLockReleasesWhenCredentialIsInvalidatedOrExpires()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId);
        refTest.AcceptPrivacyNotice("v1");
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var sessionTokenService = NewSessionTokenService(clock);
        var staleSessionToken = sessionTokenService.Create(refTest);

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        var sessionService = new RefTestSessionService();
        await using var subscriptionContext = database.CreateContext();
        await using var staleSubscription = RefTestSubscriptions.SubscribeToRefTestSessionLock(
            staleSessionToken,
            "stale-session",
            subscriptionContext,
            sessionService,
            sessionTokenService,
            clock,
            ct).GetAsyncEnumerator(ct);

        Assert.True(await staleSubscription.MoveNextAsync());
        Assert.Equal(RefTestSessionStatus.Acquired, staleSubscription.Current.Status);

        await using var rotationContext = database.CreateContext();
        var storedRefTest = await rotationContext.RefTests
            .SingleAsync(candidate => candidate.Id == refTest.Id, ct);
        storedRefTest.RegenerateToken();
        await rotationContext.SaveChangesWithRetryAsync(ct);

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.False(await staleSubscription.MoveNextAsync());

        storedRefTest.Start("v1");
        await rotationContext.SaveChangesWithRetryAsync(ct);
        var currentSessionToken = sessionTokenService.Create(storedRefTest);
        await using var currentSubscription = RefTestSubscriptions.SubscribeToRefTestSessionLock(
            currentSessionToken,
            "current-session",
            subscriptionContext,
            sessionService,
            sessionTokenService,
            clock,
            ct).GetAsyncEnumerator(ct);

        Assert.True(await currentSubscription.MoveNextAsync());
        Assert.Equal(RefTestSessionStatus.Acquired, currentSubscription.Current.Status);

        storedRefTest.SoftReset();
        await rotationContext.SaveChangesWithRetryAsync(ct);
        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.False(await currentSubscription.MoveNextAsync());

        var resetSessionToken = sessionTokenService.Create(storedRefTest);
        await using var resetSubscription = RefTestSubscriptions.SubscribeToRefTestSessionLock(
            resetSessionToken,
            "reset-session",
            subscriptionContext,
            sessionService,
            sessionTokenService,
            clock,
            ct).GetAsyncEnumerator(ct);

        Assert.True(await resetSubscription.MoveNextAsync());
        Assert.Equal(RefTestSessionStatus.Acquired, resetSubscription.Current.Status);

        clock.Advance(TimeSpan.FromHours(12));

        Assert.False(await resetSubscription.MoveNextAsync());

        var replacementSessionToken = sessionTokenService.Create(storedRefTest);
        await using var replacementSubscription = RefTestSubscriptions.SubscribeToRefTestSessionLock(
            replacementSessionToken,
            "replacement-session",
            subscriptionContext,
            sessionService,
            sessionTokenService,
            clock,
            ct).GetAsyncEnumerator(ct);

        Assert.True(await replacementSubscription.MoveNextAsync());
        Assert.Equal(RefTestSessionStatus.Acquired, replacementSubscription.Current.Status);
    }

    [Fact]
    public async Task SessionCredentialIsNotResolvedAfterAnonymization()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId);
        refTest.AcceptPrivacyNotice("v1");
        var sessionTokenService = NewSessionTokenService();
        var session = sessionTokenService.Create(refTest);

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var context = database.CreateContext();
        var storedRefTest = await context.RefTests.SingleAsync(candidate => candidate.Id == refTest.Id, ct);
        storedRefTest.Anonymize();
        await context.SaveChangesWithRetryAsync(ct);

        await Assert.ThrowsAsync<RefTestNotFoundException>(() =>
            RefTestQueries.GetRefTestByTokenAsync(
                session,
                context,
                new RefTestExpirationConfiguration(),
                sessionTokenService,
                NewJobEnqueueService(context),
                ct));
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        private readonly List<ManualTimer> _timers = [];

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
            foreach (var timer in _timers.ToArray())
                timer.FireIfDue(_utcNow);
        }

        private sealed class ManualTimer(
            ManualTimeProvider timeProvider,
            TimerCallback callback,
            object? state) : ITimer
        {
            private DateTimeOffset? _nextTick;
            private TimeSpan _period;
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;

                _period = period;
                _nextTick = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : timeProvider.GetUtcNow().Add(dueTime);
                return true;
            }

            public void Dispose() => _disposed = true;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                if (_disposed || _nextTick is not { } nextTick || nextTick > now) return;

                _nextTick = _period is { Ticks: <= 0 } || _period == Timeout.InfiniteTimeSpan
                    ? null
                    : nextTick.Add(_period);
                callback(state);
            }
        }
    }
}
