using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public interface IClientIpResolver
{
    string Resolve(HttpContext context);
}

public sealed class ConfigurableHeaderClientIpResolver(IOptions<ForwardedHeadersConfiguration> config)
    : IClientIpResolver
{
    private readonly string[] _trustedHeaders = config.Value.TrustedClientIpHeaders;

    public string Resolve(HttpContext context)
    {
        foreach (var header in _trustedHeaders)
        {
            var value = context.Request.Headers[header].FirstOrDefault();
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
