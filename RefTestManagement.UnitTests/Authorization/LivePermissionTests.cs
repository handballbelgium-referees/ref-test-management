using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class LivePermissionTests
{
    [Theory]
    [InlineData("ref-tests:create", "ref-tests:create", true)]
    [InlineData("REF-TESTS:CREATE", "ref-tests:create", true)]
    [InlineData("ref-tests:*", "ref-tests:create", true)]
    [InlineData("superadmin", "ref-tests:create", true)]
    [InlineData("ref-tests:view", "ref-tests:create", false)]
    [InlineData("question:*", "ref-tests:create", false)]
    [InlineData("ref-tests", "ref-tests:create", false)]
    public void MatcherResolvesExactWildcardAndSuperadmin(string granted, string required, bool expected)
    {
        Assert.Equal(expected, PermissionMatcher.IsSatisfied([granted], required));
    }

    [Fact]
    public void MatcherDeniesWhenNothingGranted()
    {
        Assert.False(PermissionMatcher.IsSatisfied([], "ref-tests:create"));
    }

    [Fact]
    public async Task VerifierAllowsWhenProviderGrantsPermission()
    {
        var verifier = CreateVerifier(new FakeSync(granted: ["ref-tests:create"]), "2");

        Assert.True(await Check(verifier, UserWithSub("user-1")));
    }

    [Fact]
    public async Task VerifierDeniesWhenProviderDoesNotGrantPermission()
    {
        var verifier = CreateVerifier(new FakeSync(granted: ["ref-tests:view"]), "2");

        Assert.False(await Check(verifier, UserWithSub("user-1")));
    }

    [Fact]
    public async Task VerifierUsesNameIdentifierWhenSubIsMissing()
    {
        var sync = new FakeSync(granted: ["ref-tests:create"]);
        var verifier = CreateVerifier(sync, "2");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-2")], "test"));

        Assert.True(await Check(verifier, user));
        Assert.Equal("user-2", sync.LastUserId);
    }

    [Fact]
    public async Task VerifierDeniesUserWithoutSubject()
    {
        var sync = new FakeSync(granted: ["ref-tests:create"]);
        var verifier = CreateVerifier(sync, "2");

        Assert.False(await Check(verifier, new ClaimsPrincipal(new ClaimsIdentity())));
        Assert.Equal(0, sync.CallCount);
    }

    [Fact]
    public async Task VerifierDeniesWhenProviderIsNotRegistered()
    {
        var verifier = CreateVerifier(null, "2");

        Assert.False(await Check(verifier, UserWithSub("user-1")));
    }

    [Fact]
    public async Task VerifierDeniesWhenProviderThrows()
    {
        var verifier = CreateVerifier(new FakeSync(throwOnRead: true), "2");

        Assert.False(await Check(verifier, UserWithSub("user-1")));
    }

    [Fact]
    public async Task VerifierDeniesWhenProviderExceedsTimeout()
    {
        var verifier = CreateVerifier(new FakeSync(hang: true), "1");

        Assert.False(await Check(verifier, UserWithSub("user-1")));
    }

    [Fact]
    public async Task VerifierRethrowsCallerCancellation()
    {
        var verifier = CreateVerifier(new FakeSync(hang: true), "30");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => verifier.HasPermissionAsync(UserWithSub("user-1"), "ref-tests:create", cts.Token));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void VerifierRejectsNonPositiveTimeout(string seconds)
    {
        Assert.Throws<InvalidOperationException>(() => CreateVerifier(new FakeSync(), seconds));
    }

    [Fact]
    public async Task HandlerSkipsProviderWhenCacheDeniesPermission()
    {
        var verifier = new FakeVerifier(allow: true);
        var context = HandlerContext(cached: ["ref-tests:view"]);

        await new LivePermissionHandler(verifier).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Equal(0, verifier.CallCount);
    }

    [Fact]
    public async Task HandlerSucceedsWhenCacheAndProviderGrantPermission()
    {
        var verifier = new FakeVerifier(allow: true);
        var context = HandlerContext(cached: ["ref-tests:create"]);

        await new LivePermissionHandler(verifier).HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal(1, verifier.CallCount);
    }

    [Fact]
    public async Task HandlerFailsWhenProviderRevokesPermissionStillCachedAsGranted()
    {
        var verifier = new FakeVerifier(allow: false);
        var context = HandlerContext(cached: ["ref-tests:create"]);

        await new LivePermissionHandler(verifier).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Equal(1, verifier.CallCount);
    }

    private static Task<bool> Check(ILivePermissionVerifier verifier, ClaimsPrincipal user) =>
        verifier.HasPermissionAsync(user, "ref-tests:create", TestContext.Current.CancellationToken);

    private static AuthorizationHandlerContext HandlerContext(string[] cached)
    {
        var claims = cached.Select(p => new Claim(
            TaskPermissionHandler.PermissionClaimType, p));
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-1"), .. claims], "test"));
        return new AuthorizationHandlerContext(
            [new LivePermissionRequirement("ref-tests:create")], user, null);
    }

    private static ClaimsPrincipal UserWithSub(string sub) =>
        new(new ClaimsIdentity([new Claim("sub", sub)], "test"));

    private static LivePermissionVerifier CreateVerifier(IIdentityPermissionSync? sync, string timeoutSeconds)
    {
        var services = new ServiceCollection();
        if (sync is not null)
            services.AddSingleton(sync);

        var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LivePermissionVerifier.LivePermissionTimeoutSecondsKey] = timeoutSeconds
            })
            .Build();

        return new LivePermissionVerifier(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LivePermissionVerifier>.Instance,
            configuration);
    }

    private sealed class FakeVerifier(bool allow) : ILivePermissionVerifier
    {
        public int CallCount { get; private set; }

        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string requiredPermission,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(allow);
        }
    }

    private sealed class FakeSync(
        IEnumerable<string>? granted = null,
        bool throwOnRead = false,
        bool hang = false) : IIdentityPermissionSync
    {
        public bool IsConfigured => true;
        public int CallCount { get; private set; }
        public string? LastUserId { get; private set; }

        public Task SyncPermissionsAsync(IReadOnlyList<string> permissions, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUserId = userId;

            if (throwOnRead)
                throw new InvalidOperationException("provider down");

            if (hang)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            return new HashSet<string>(granted ?? [], StringComparer.OrdinalIgnoreCase);
        }
    }
}
