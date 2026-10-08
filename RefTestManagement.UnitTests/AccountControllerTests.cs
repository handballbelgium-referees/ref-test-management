using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api;
using Handball.Belgium.RefTestManagement.Api.Controllers;
using Handball.Belgium.RefTestManagement.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task GetPermissionsReturnsServiceUnavailableWhenSnapshotIsUnavailable()
    {
        var controller = new AccountController(
            new NullPermissionSnapshotService(),
            CreateEnvironment("Production"))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = Assert.IsType<StatusCodeResult>(
            await controller.GetPermissions(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [Theory]
    [InlineData(
        "Development",
        "http://localhost:4200/ref-tests?status=completed#/results",
        "http://localhost:4200/ref-tests?status=completed#/results")]
    [InlineData("Development", "https://attacker.example/path", "/")]
    [InlineData("Development", "http://localhost.attacker.example:4200/path", "/")]
    [InlineData("Development", "http://localhost:4201/path", "/")]
    [InlineData("Production", "http://localhost:4200/ref-tests", "/")]
    [InlineData("Production", "/ref-tests?status=completed", "/ref-tests?status=completed")]
    public async Task LoginOnlyAllowsLocalDevelopmentUiReturnUrls(
        string environmentName,
        string returnUrl,
        string expectedRedirectUri)
    {
        var authenticationService = new CapturingAuthenticationService();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticationService)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var controller = new AccountController(
            new NullPermissionSnapshotService(),
            CreateEnvironment(environmentName))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
        controller.Url = new UrlHelper(new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor()));

        await controller.Login(returnUrl);

        Assert.Equal("Auth0", authenticationService.ChallengedScheme);
        Assert.Equal(expectedRedirectUri, authenticationService.ChallengedProperties?.RedirectUri);
    }

    [Fact]
    public async Task LogoutBuildsPostLogoutTargetFromConfiguredOriginInsteadOfRequestHost()
    {
        var configuration = CreateAuthConfiguration("https://ref-test.example");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecurityConfiguration(configuration);
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get("Auth0");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("attacker.example");
        httpContext.Request.PathBase = new PathString("/application");
        var redirectContext = new RedirectContext(
            httpContext,
            new AuthenticationScheme("Auth0", "Auth0", typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationProperties { RedirectUri = "/ref-tests" });

        await options.Events.OnRedirectToIdentityProviderForSignOut(redirectContext);

        var location = httpContext.Response.Headers.Location.ToString();
        Assert.Contains(
            "returnTo=https%3A%2F%2Fref-test.example%2Fapplication%2Fref-tests",
            location);
        Assert.DoesNotContain("attacker.example", location);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-origin")]
    [InlineData("http://ref-test.example")]
    [InlineData("https://ref-test.example/path")]
    [InlineData("https://ref-test.example?query=1")]
    [InlineData("https://ref-test.example#fragment")]
    [InlineData("https://user@ref-test.example")]
    public void SecurityConfigurationRejectsMissingOrInvalidPublicOrigin(string? publicOrigin)
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddSecurityConfiguration(CreateAuthConfiguration(publicOrigin)));
    }

    private static IConfiguration CreateAuthConfiguration(string? publicOrigin) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth0:Domain"] = "tenant.auth0.example",
                ["Auth0:ClientId"] = "test-client",
                ["Auth0:PublicOrigin"] = publicOrigin
            })
            .Build();

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = Host.CreateApplicationBuilder().Environment;
        environment.EnvironmentName = environmentName;
        return environment;
    }

    private sealed class CapturingAuthenticationService : IAuthenticationService
    {
        public AuthenticationProperties? ChallengedProperties { get; private set; }
        public string? ChallengedScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            throw new NotSupportedException();

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            ChallengedScheme = scheme;
            ChallengedProperties = properties;
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            throw new NotSupportedException();
    }

    private sealed class NullPermissionSnapshotService : IPermissionSnapshotService
    {
        public Task<IReadOnlySet<string>?> GetCurrentPermissionsAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>?>(null);
    }
}
