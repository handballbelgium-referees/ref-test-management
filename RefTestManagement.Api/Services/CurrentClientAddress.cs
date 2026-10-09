using Microsoft.AspNetCore.Http;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>Resolves the current request's client address for anonymous privacy rate limits.</summary>
public interface ICurrentClientAddress
{
    string Resolve();
}

public sealed class CurrentClientAddress(
    IHttpContextAccessor httpContextAccessor,
    IClientIpResolver clientIpResolver) : ICurrentClientAddress
{
    public string Resolve() =>
        httpContextAccessor.HttpContext is { } httpContext
            ? clientIpResolver.Resolve(httpContext)
            : "unknown";
}
