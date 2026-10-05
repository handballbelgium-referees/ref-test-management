using System.Net;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public interface IClientIpResolver
{
    string Resolve(HttpContext context);
}

public sealed class ConfigurableHeaderClientIpResolver(IOptionsMonitor<ForwardedHeadersConfiguration> config)
    : IClientIpResolver
{
    internal static readonly object TransportPeerAddressItemKey = new();

    internal static void ValidateConfiguration(ForwardedHeadersConfiguration configuration)
    {
        if (configuration.TrustedClientIpHeaders.Length == 0 ||
            configuration.KnownProxies.Length > 0 ||
            configuration.KnownNetworks.Length > 0)
            return;

        throw new InvalidOperationException(
            "ForwardedHeadersConfiguration:TrustedClientIpHeaders requires at least one ForwardedHeadersConfiguration:KnownProxies or KnownNetworks entry. Configure the actual ingress proxy address/CIDR; header-only client-IP configuration is not accepted.");
    }

    public string Resolve(HttpContext context)
    {
        var configuration = config.CurrentValue;
        var transportPeerAddress = context.Items.TryGetValue(TransportPeerAddressItemKey, out var originalPeer)
            ? originalPeer as IPAddress
            : context.Connection.RemoteIpAddress;

        if (IsTrustedPeer(transportPeerAddress, configuration))
        {
            foreach (var header in configuration.TrustedClientIpHeaders)
            {
                // ForwardedHeaders has already resolved X-Forwarded-For into RemoteIpAddress.
                if (string.Equals(header, "X-Forwarded-For", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.Connection.RemoteIpAddress is { } remoteAddress)
                        return remoteAddress.ToString();

                    continue;
                }

                var values = context.Request.Headers[header];
                if (values.Count == 1 && IPAddress.TryParse(values[0], out var address))
                    return address.ToString();
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool IsTrustedPeer(IPAddress? address, ForwardedHeadersConfiguration configuration)
    {
        if (address is null)
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return configuration.KnownProxies.Any(value =>
                   IPAddress.TryParse(value, out var proxy) && address.Equals(proxy)) ||
               configuration.KnownNetworks.Any(value =>
                   IPNetwork.TryParse(value, out var network) && network.Contains(address));
    }
}
