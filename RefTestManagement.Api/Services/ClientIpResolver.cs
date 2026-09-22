using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public interface IClientIpResolver
{
    string Resolve(HttpContext context);
}

public sealed class ConfigurableHeaderClientIpResolver(IOptionsMonitor<ForwardedHeadersConfiguration> config)
    : IClientIpResolver
{
    public string Resolve(HttpContext context)
    {
        foreach (var header in config.CurrentValue.TrustedClientIpHeaders)
        {
            var value = context.Request.Headers[header].FirstOrDefault();
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
