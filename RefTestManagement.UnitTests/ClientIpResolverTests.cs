using System.Net;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class ClientIpResolverTests
{
    [Fact]
    public void ResolveReturnsFirstConfiguredTrustedHeaderValue()
    {
        var resolver = CreateResolver(["X-Azure-ClientIP", "X-Forwarded-For"]);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";
        context.Request.Headers["X-Azure-ClientIP"] = "198.51.100.10";

        var resolved = resolver.Resolve(context);

        Assert.Equal("198.51.100.10", resolved);
    }

    [Fact]
    public void ResolveFallsBackToRemoteIpAddressWhenTrustedHeadersAreMissing()
    {
        var resolver = CreateResolver(["X-Azure-ClientIP"]);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.77");

        var resolved = resolver.Resolve(context);

        Assert.Equal("192.0.2.77", resolved);
    }

    [Fact]
    public void ResolveReturnsUnknownWhenNoTrustedHeaderOrRemoteAddressIsAvailable()
    {
        var resolver = CreateResolver(["X-Azure-ClientIP"]);

        var resolved = resolver.Resolve(new DefaultHttpContext());

        Assert.Equal("unknown", resolved);
    }

    private static IClientIpResolver CreateResolver(string[] trustedHeaders) =>
        new ConfigurableHeaderClientIpResolver(
            Options.Create(new ForwardedHeadersConfiguration { TrustedClientIpHeaders = trustedHeaders }));
}
