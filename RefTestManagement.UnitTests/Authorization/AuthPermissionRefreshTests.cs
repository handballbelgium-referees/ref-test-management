using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api;
using Handball.Belgium.RefTestManagement.Api.Controllers;
using Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class AuthPermissionRefreshTests
{
    [Fact]
    public async Task CookieValidationReplacesStaleClaimsAndRenewsOnlyWhenTheyChange()
    {
        var snapshotService = new FakePermissionSnapshotService(
            (_, _) => Task.FromResult<IReadOnlySet<string>?>(Set(Permissions.RefTests.ViewList)));
        using var provider = CreateCookieProvider(snapshotService);
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var originalPrincipal = AuthenticatedUser(
            new Claim("permissions", Permissions.RefTests.Create));

        var firstContext = CreateCookieContext(provider, options, originalPrincipal);
        await options.Events.OnValidatePrincipal(firstContext);

        Assert.True(firstContext.ShouldRenew);
        Assert.Equal(
            new[] { Permissions.RefTests.ViewList },
            firstContext.Principal!.FindAll("permissions").Select(claim => claim.Value));

        var unchangedContext = CreateCookieContext(provider, options, firstContext.Principal);
        await options.Events.OnValidatePrincipal(unchangedContext);
        Assert.False(unchangedContext.ShouldRenew);
    }

    [Fact]
    public async Task CookieValidationRejectsPrincipalWhenAStaleSnapshotCannotBeRefreshed()
    {
        var snapshotService = new FakePermissionSnapshotService(
            (_, _) => Task.FromResult<IReadOnlySet<string>?>(null));
        using var provider = CreateCookieProvider(snapshotService);
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var context = CreateCookieContext(
            provider,
            options,
            AuthenticatedUser(new Claim("permissions", Permissions.Superadmin)));

        await options.Events.OnValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task JwtValidationReplacesTokenPermissionsWithCurrentGrants()
    {
        var snapshotService = new FakePermissionSnapshotService(
            (_, _) => Task.FromResult<IReadOnlySet<string>?>(
                Set(Permissions.RefTests.ViewList)));
        using var provider = CreateCookieProvider(snapshotService);
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var context = new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = AuthenticatedUser(new Claim("permissions", Permissions.Superadmin))
        };

        await options.Events.OnTokenValidated(context);

        Assert.Null(context.Result);
        Assert.Equal(
            new[] { Permissions.RefTests.ViewList },
            context.Principal!.FindAll("permissions").Select(claim => claim.Value));
    }

    [Fact]
    public async Task AccountPermissionsReturnsFreshSortedGrantsAndFailsClosedWhenUnavailable()
    {
        var freshSnapshot = new FakePermissionSnapshotService(
            (_, _) => Task.FromResult<IReadOnlySet<string>?>(
                Set(Permissions.RefTests.ViewList, Permissions.RefTests.Create)));
        var controller = CreateAccountController(freshSnapshot);

        var response = Assert.IsType<OkObjectResult>(
            await controller.GetPermissions(CancellationToken.None));
        Assert.Equal(
            new[] { Permissions.RefTests.Create, Permissions.RefTests.ViewList },
            Assert.IsType<string[]>(response.Value));

        controller = CreateAccountController(new FakePermissionSnapshotService(
            (_, _) => Task.FromResult<IReadOnlySet<string>?>(null)));
        Assert.IsType<StatusCodeResult>(
            await controller.GetPermissions(CancellationToken.None));
    }

    [Fact]
    public void AdminResolversRejectUnsupportedEventMessages()
    {
        var unsupportedMessage = RefTestSubscriptionMessage.Create(new object());

        Assert.Throws<InvalidOperationException>(() =>
            RefTestSubscriptions.RefTestsUpdated(unsupportedMessage));
        Assert.Throws<InvalidOperationException>(() =>
            RefTestSubscriptions.RefTestUpdated(unsupportedMessage));
    }

    private static ServiceProvider CreateCookieProvider(IPermissionSnapshotService snapshotService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPermissionSnapshotService>(snapshotService);
        services.AddSecurityConfiguration(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateAuthorizationProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTaskBasedAuthorization();
        return services.BuildServiceProvider();
    }

    private static AccountController CreateAccountController(IPermissionSnapshotService snapshotService)
    {
        var environment = Host.CreateApplicationBuilder().Environment;
        environment.EnvironmentName = Environments.Production;
        var controller = new AccountController(snapshotService, environment, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = AuthenticatedUser() }
            }
        };
        return controller;
    }

    private static CookieValidatePrincipalContext CreateCookieContext(
        IServiceProvider provider,
        CookieAuthenticationOptions options,
        ClaimsPrincipal principal)
    {
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CookieAuthenticationDefaults.AuthenticationScheme,
            typeof(CookieAuthenticationHandler));
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties(),
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new CookieValidatePrincipalContext(httpContext, scheme, options, ticket);
    }

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims) =>
        new(new ClaimsIdentity(
            [new Claim("sub", "auth0|permission-test"), .. claims],
            "unit-test"));

    private static IReadOnlySet<string> Set(params string[] permissions) =>
        new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

    private sealed class FakePermissionSnapshotService(
        Func<ClaimsPrincipal, CancellationToken, Task<IReadOnlySet<string>?>> getPermissions)
        : IPermissionSnapshotService
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<IReadOnlySet<string>?> GetCurrentPermissionsAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return getPermissions(principal, cancellationToken);
        }

        public Task<IReadOnlySet<string>?> RefreshForSubjectAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default) =>
            GetCurrentPermissionsAsync(principal, cancellationToken);
    }
}
