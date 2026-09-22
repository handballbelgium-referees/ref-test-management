namespace Handball.Belgium.RefTestManagement.Application.Configurations;

public sealed class ForwardedHeadersConfiguration
{
    public int ForwardLimit { get; init; } = 1;
    public string[] KnownProxies { get; init; } = [];
    public string[] KnownNetworks { get; init; } = [];
    public bool AllowUnsafeRateLimitingWithoutTrustedForwarders { get; init; }
}
