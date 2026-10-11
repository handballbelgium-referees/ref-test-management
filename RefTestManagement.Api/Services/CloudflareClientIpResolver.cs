using System.Net;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public sealed class CloudflareClientIpResolver : IClientIpResolver
{
    internal static readonly object TrustedClientIpItemKey = new();

    public string Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var address = context.Items.TryGetValue(TrustedClientIpItemKey, out var assertedAddress)
            ? assertedAddress as IPAddress
            : null;
        address ??= context.Connection.RemoteIpAddress;

        if (address is null)
            return "unknown";

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.ToString();
    }
}
