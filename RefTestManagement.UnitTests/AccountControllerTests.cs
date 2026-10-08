using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Controllers;
using Handball.Belgium.RefTestManagement.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
