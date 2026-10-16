using System.Net;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Api.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class ClientIpResolverTests
{
    [Fact]
    public void ResolveReturnsFirstConfiguredTrustedHeaderValueFromKnownProxy()
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP", "X-Forwarded-For"],
            KnownProxies = ["10.0.0.5"]
        });
        var context = CreateContext(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("203.0.113.42"));
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";
        context.Request.Headers["X-Azure-ClientIP"] = "198.51.100.10";

        var resolved = resolver.Resolve(context);

        Assert.Equal("198.51.100.10", resolved);
    }

    [Fact]
    public void ResolveUsesForwardedRemoteAddressWhenXForwardedForPrecedesCustomHeader()
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Forwarded-For", "X-Azure-ClientIP"],
            KnownProxies = ["10.0.0.5"]
        });
        var context = CreateContext(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("203.0.113.42"));
        context.Request.Headers["X-Forwarded-For"] = "192.0.2.77";
        context.Request.Headers["X-Azure-ClientIP"] = "198.51.100.10";

        var resolved = resolver.Resolve(context);

        Assert.Equal("203.0.113.42", resolved);
    }

    [Theory]
    [InlineData("198.51.100.10")]
    [InlineData("198.51.100.10, 203.0.113.42")]
    public void ResolveUsesForwardedRemoteAddressInsteadOfRawXForwardedFor(string forwardedFor)
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Forwarded-For"],
            KnownProxies = ["10.0.0.5"]
        });
        var context = CreateContext(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("203.0.113.42"));
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;

        var resolved = resolver.Resolve(context);

        Assert.Equal("203.0.113.42", resolved);
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("198.51.100.10, 203.0.113.42")]
    public void ResolveFallsBackToResolvedRemoteAddressForInvalidOrListValuedCustomHeader(string headerValue)
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP"],
            KnownProxies = ["10.0.0.5"]
        });
        var context = CreateContext(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("203.0.113.42"));
        context.Request.Headers["X-Azure-ClientIP"] = headerValue;

        var resolved = resolver.Resolve(context);

        Assert.Equal("203.0.113.42", resolved);
    }

    [Fact]
    public void ResolveFallsBackToResolvedRemoteAddressForMultipleCustomHeaderValues()
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP"],
            KnownProxies = ["10.0.0.5"]
        });
        var context = CreateContext(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("203.0.113.42"));
        context.Request.Headers["X-Azure-ClientIP"] =
            new StringValues(new[] { "198.51.100.10", "203.0.113.42" });

        var resolved = resolver.Resolve(context);

        Assert.Equal("203.0.113.42", resolved);
    }

    [Fact]
    public void ResolveIgnoresConfiguredHeaderFromUntrustedTransportPeer()
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP"],
            KnownProxies = ["10.0.0.5"]
        });
        var context = CreateContext(IPAddress.Parse("192.0.2.77"));
        context.Request.Headers["X-Azure-ClientIP"] = "198.51.100.10";

        var resolved = resolver.Resolve(context);

        Assert.Equal("192.0.2.77", resolved);
    }

    [Fact]
    public void ResolveAcceptsConfiguredHeaderFromKnownNetwork()
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP"],
            KnownNetworks = ["10.0.0.0/24"]
        });
        var context = CreateContext(IPAddress.Parse("10.0.0.42"), IPAddress.Parse("203.0.113.42"));
        context.Request.Headers["X-Azure-ClientIP"] = "198.51.100.10";

        var resolved = resolver.Resolve(context);

        Assert.Equal("198.51.100.10", resolved);
    }

    [Fact]
    public void ValidateConfigurationRejectsLegacyHeaderOnlyConfiguration()
    {
        var configuration = new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP"]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ConfigurableHeaderClientIpResolver.ValidateConfiguration(configuration));

        Assert.Contains("KnownProxies", exception.Message);
        Assert.Contains("KnownNetworks", exception.Message);
    }

    [Fact]
    public void ResolveFallsBackToRemoteIpAddressWhenTrustedHeadersAreMissing()
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP"],
            KnownProxies = ["10.0.0.5"]
        });
        var context = CreateContext(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("192.0.2.77"));

        var resolved = resolver.Resolve(context);

        Assert.Equal("192.0.2.77", resolved);
    }

    [Fact]
    public void ResolveReturnsUnknownWhenNoTrustedHeaderOrRemoteAddressIsAvailable()
    {
        var resolver = CreateResolver(new ForwardedHeadersConfiguration
        {
            TrustedClientIpHeaders = ["X-Azure-ClientIP"]
        });

        var resolved = resolver.Resolve(new DefaultHttpContext());

        Assert.Equal("unknown", resolved);
    }

    private static IClientIpResolver CreateResolver(ForwardedHeadersConfiguration configuration) =>
        new ConfigurableHeaderClientIpResolver(
            new OptionsMonitorStub(configuration));

    private static DefaultHttpContext CreateContext(IPAddress transportPeer, IPAddress? forwardedAddress = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = transportPeer;
        context.Items[ConfigurableHeaderClientIpResolver.TransportPeerAddressItemKey] = transportPeer;
        context.Connection.RemoteIpAddress = forwardedAddress ?? transportPeer;
        return context;
    }

    private sealed class OptionsMonitorStub(ForwardedHeadersConfiguration value)
        : IOptionsMonitor<ForwardedHeadersConfiguration>
    {
        public ForwardedHeadersConfiguration CurrentValue => value;
        public ForwardedHeadersConfiguration Get(string? name) => value;
        public IDisposable? OnChange(Action<ForwardedHeadersConfiguration, string?> listener) => null;
    }
}
