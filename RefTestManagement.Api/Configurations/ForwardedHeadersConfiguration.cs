using System.ComponentModel.DataAnnotations;

namespace Handball.Belgium.RefTestManagement.Api.Configurations;

public sealed class ForwardedHeadersConfiguration
{
    [Range(1, int.MaxValue)]
    public int ForwardLimit { get; init; } = 1;
    public string[] TrustedClientIpHeaders { get; init; } = [];
    public string[] KnownProxies { get; init; } = [];
    public string[] KnownNetworks { get; init; } = [];
    public bool AllowUnsafeRateLimitingWithoutTrustedForwarders { get; init; }
}
