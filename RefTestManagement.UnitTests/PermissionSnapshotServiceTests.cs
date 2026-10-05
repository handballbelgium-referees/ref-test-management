using System.Net;
using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Auth0.Models;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PermissionSnapshotServiceTests
{
    [Fact]
    public async Task SnapshotRefreshesAtFourMinuteBoundaryAndPicksUpRevocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-04T17:00:00Z"));
        IReadOnlySet<string> currentPermissions = Set("ref-tests:create");
        var managementService = new FakeAuth0ManagementService((_, _) =>
            Task.FromResult(currentPermissions));
        using var provider = CreateProvider(managementService, clock);
        var permissionService = provider.GetRequiredService<IPermissionSnapshotService>();
        var user = AuthenticatedUser("auth0|freshness-test");

        var initial = await permissionService.GetCurrentPermissionsAsync(user, cancellationToken);
        Assert.Contains("ref-tests:create", initial!);

        currentPermissions = Set("ref-tests:view-list");
        clock.Advance(TimeSpan.FromMinutes(4) - TimeSpan.FromTicks(1));
        var stillFresh = await permissionService.GetCurrentPermissionsAsync(user, cancellationToken);
        Assert.Contains("ref-tests:create", stillFresh!);
        Assert.Equal(1, managementService.PermissionCalls);

        clock.Advance(TimeSpan.FromTicks(1));
        var refreshed = await permissionService.GetCurrentPermissionsAsync(user, cancellationToken);
        Assert.Contains("ref-tests:view-list", refreshed!);
        Assert.DoesNotContain("ref-tests:create", refreshed!);
        Assert.Equal(2, managementService.PermissionCalls);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task FailedRefreshNeverReturnsStaleGrantsAndSuppressesImmediateRetries(
        HttpStatusCode statusCode)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-04T17:00:00Z"));
        var shouldFail = false;
        var managementService = new FakeAuth0ManagementService((_, _) =>
            shouldFail
                ? Task.FromException<IReadOnlySet<string>>(
                    new HttpRequestException("Auth0 request failed", null, statusCode))
                : Task.FromResult(Set("ref-tests:delete")));
        using var provider = CreateProvider(managementService, clock);
        var permissionService = provider.GetRequiredService<IPermissionSnapshotService>();
        var user = AuthenticatedUser("auth0|failure-test");

        Assert.Contains(
            "ref-tests:delete",
            (await permissionService.GetCurrentPermissionsAsync(user, cancellationToken))!);
        clock.Advance(TimeSpan.FromMinutes(4));
        shouldFail = true;

        Assert.Null(await permissionService.GetCurrentPermissionsAsync(user, cancellationToken));
        Assert.Null(await permissionService.GetCurrentPermissionsAsync(user, cancellationToken));
        Assert.Equal(2, managementService.PermissionCalls);

        clock.Advance(TimeSpan.FromSeconds(30));
        shouldFail = false;
        var recovered = await permissionService.GetCurrentPermissionsAsync(user, cancellationToken);

        Assert.Contains("ref-tests:delete", recovered!);
        Assert.Equal(3, managementService.PermissionCalls);
    }

    [Fact]
    public async Task ConcurrentChecksForOneUserShareOneManagementApiRefresh()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-04T17:00:00Z"));
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlySet<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var managementService = new FakeAuth0ManagementService((_, _) =>
        {
            started.TrySetResult(true);
            return release.Task;
        });
        using var provider = CreateProvider(managementService, clock);
        var permissionService = provider.GetRequiredService<IPermissionSnapshotService>();
        var user = AuthenticatedUser("auth0|single-flight-test");

        var checks = Enumerable.Range(0, 32)
            .Select(_ => permissionService.GetCurrentPermissionsAsync(user, cancellationToken))
            .ToArray();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.Equal(1, managementService.PermissionCalls);
        release.SetResult(Set("ref-tests:view-detail"));

        var snapshots = await Task.WhenAll(checks);
        Assert.All(snapshots, snapshot => Assert.Contains("ref-tests:view-detail", snapshot!));
        Assert.Equal(1, managementService.PermissionCalls);
    }

    [Fact]
    public async Task CallerCancellationStopsWaitingWithoutCancellingSharedRefresh()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlySet<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCancellationToken = CancellationToken.None;
        var managementService = new FakeAuth0ManagementService((_, token) =>
        {
            refreshCancellationToken = token;
            started.TrySetResult(true);
            return release.Task;
        });
        using var provider = CreateProvider(managementService, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var permissionService = provider.GetRequiredService<IPermissionSnapshotService>();
        var user = AuthenticatedUser("auth0|cancellation-test");
        using var callerCancellation = new CancellationTokenSource();

        var canceledWait = permissionService.GetCurrentPermissionsAsync(user, callerCancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        var survivingWait = permissionService.GetCurrentPermissionsAsync(user, cancellationToken);

        callerCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledWait);
        Assert.False(refreshCancellationToken.IsCancellationRequested);

        release.SetResult(Set("ref-tests:view-detail"));
        Assert.Contains("ref-tests:view-detail", (await survivingWait)!);
        Assert.Equal(1, managementService.PermissionCalls);
    }

    [Fact]
    public async Task PendingRefreshCapacityFailsClosedAndReleasesSlotsAfterCompletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-04T17:00:00Z"));
        var cachedUserId = "auth0|expired-capacity-test";
        var release = new TaskCompletionSource<IReadOnlySet<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new SemaphoreSlim(0);
        var firstCachedResponse = 0;
        var managementService = new FakeAuth0ManagementService((userId, _) =>
        {
            if (userId == cachedUserId && Interlocked.Exchange(ref firstCachedResponse, 1) == 0)
                return Task.FromResult(Set("ref-tests:stale"));

            started.Release();
            return release.Task;
        });
        using var provider = CreateProvider(managementService, clock);
        var permissionService = provider.GetRequiredService<IPermissionSnapshotService>();
        var cachedUser = AuthenticatedUser(cachedUserId);

        Assert.Contains(
            "ref-tests:stale",
            (await permissionService.GetCurrentPermissionsAsync(cachedUser, cancellationToken))!);
        clock.Advance(TimeSpan.FromMinutes(5));

        var pendingRefreshes = Enumerable.Range(0, PermissionSnapshotService.MaximumPendingRefreshes)
            .Select(index => permissionService.GetCurrentPermissionsAsync(
                AuthenticatedUser($"auth0|pending-{index}"),
                cancellationToken))
            .ToArray();
        for (var index = 0; index < PermissionSnapshotService.MaximumConcurrentRefreshes; index++)
            await started.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        var rejectedRefresh = permissionService.GetCurrentPermissionsAsync(cachedUser, cancellationToken);
        release.SetResult(Set("ref-tests:current"));
        await Task.WhenAll(pendingRefreshes);

        Assert.Null(await rejectedRefresh);
        var recovered = await permissionService.GetCurrentPermissionsAsync(cachedUser, cancellationToken);
        Assert.Contains("ref-tests:current", recovered!);
        Assert.DoesNotContain("ref-tests:stale", recovered!);
        Assert.Equal(PermissionSnapshotService.MaximumPendingRefreshes + 2, managementService.PermissionCalls);
    }

    [Fact]
    public async Task RefreshesAcrossUsersAreLimitedPerApiProcess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-04T17:00:00Z"));
        var started = new SemaphoreSlim(0);
        var release = new TaskCompletionSource<IReadOnlySet<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var activeRefreshes = 0;
        var maximumActiveRefreshes = 0;
        var managementService = new FakeAuth0ManagementService(async (_, _) =>
        {
            var active = Interlocked.Increment(ref activeRefreshes);
            while (true)
            {
                var previousMaximum = Volatile.Read(ref maximumActiveRefreshes);
                if (active <= previousMaximum ||
                    Interlocked.CompareExchange(ref maximumActiveRefreshes, active, previousMaximum) ==
                    previousMaximum)
                {
                    break;
                }
            }

            started.Release();
            try
            {
                return await release.Task;
            }
            finally
            {
                Interlocked.Decrement(ref activeRefreshes);
            }
        });
        using var provider = CreateProvider(managementService, clock);
        var permissionService = provider.GetRequiredService<IPermissionSnapshotService>();

        var checks = Enumerable.Range(0, 12)
            .Select(index => permissionService.GetCurrentPermissionsAsync(
                AuthenticatedUser($"auth0|user-{index}"),
                cancellationToken))
            .ToArray();
        for (var index = 0; index < PermissionSnapshotService.MaximumConcurrentRefreshes; index++)
            await started.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.Equal(PermissionSnapshotService.MaximumConcurrentRefreshes, maximumActiveRefreshes);
        release.SetResult(Set("ref-tests:view-list"));
        await Task.WhenAll(checks);

        Assert.Equal(12, managementService.PermissionCalls);
        Assert.InRange(maximumActiveRefreshes, 1, PermissionSnapshotService.MaximumConcurrentRefreshes);
    }

    [Fact]
    public async Task CacheEvictsSnapshotsAtItsEntryLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-04T17:00:00Z"));
        var managementService = new FakeAuth0ManagementService((_, _) =>
            Task.FromResult(Set("ref-tests:view-list")));
        using var provider = CreateProvider(managementService, clock);
        var permissionService = provider.GetRequiredService<IPermissionSnapshotService>();
        var users = Enumerable.Range(0, PermissionSnapshotService.MaximumCachedSnapshots + 1)
            .Select(index => AuthenticatedUser($"auth0|cache-capacity-{index}"))
            .ToArray();

        foreach (var user in users)
            Assert.Contains(
                "ref-tests:view-list",
                (await permissionService.GetCurrentPermissionsAsync(user, cancellationToken))!);

        var callsBeforeEvictionCheck = managementService.PermissionCalls;
        foreach (var user in users)
        {
            await permissionService.GetCurrentPermissionsAsync(user, cancellationToken);
            if (managementService.PermissionCalls > callsBeforeEvictionCheck)
                break;
        }

        Assert.Equal(callsBeforeEvictionCheck + 1, managementService.PermissionCalls);
    }

    private static ServiceProvider CreateProvider(
        FakeAuth0ManagementService managementService,
        TimeProvider timeProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<IAuth0ManagementService>(managementService);
        services.AddSingleton<IPermissionSnapshotService, PermissionSnapshotService>();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal AuthenticatedUser(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "unit-test"));

    private static IReadOnlySet<string> Set(params string[] permissions) =>
        new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now += amount;
    }

    private sealed class FakeAuth0ManagementService(
        Func<string, CancellationToken, Task<IReadOnlySet<string>>> getPermissions)
        : IAuth0ManagementService
    {
        private int _permissionCalls;

        public int PermissionCalls => Volatile.Read(ref _permissionCalls);

        public Task<IReadOnlySet<string>> GetUserPermissionsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _permissionCalls);
            return getPermissions(userId, cancellationToken);
        }

        public Task<IReadOnlyList<Auth0User>> GetUsersWithPermissionAsync(
            string permission,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SyncPermissionsAsync(
            IReadOnlyList<string> permissions,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
