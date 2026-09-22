using Handball.Belgium.RefTestManagement.Auth0.Configurations;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Auth0;

public static class Auth0ServiceExtensions
{
    public static IServiceCollection AddAuth0ManagementServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<Auth0ManagementConfiguration>(options =>
        {
            // Reuse the shared Auth0 section for Domain and Audience
            var auth0Section = configuration.GetSection("Auth0");
            options.Domain = auth0Section["Domain"] ?? string.Empty;
            options.Audience = auth0Section["Audience"] ?? string.Empty;
            options.ManagementClientId = auth0Section["ManagementClientId"] ?? string.Empty;
            options.ManagementClientSecret = auth0Section["ManagementClientSecret"] ?? string.Empty;
        });

        services.AddSingleton<Auth0ManagementTokenCache>();

        services.AddHttpClient<IAuth0ManagementService, Auth0ManagementService>(client =>
            {
                // Without this the client inherits HttpClient's 100-second default, which is long
                // enough that a hung Auth0 tenant stalls permission sync for most of two minutes.
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            // Every call this client makes is safe to repeat: the token request issues a fresh
            // token, the reads are reads, and the one PATCH sends the complete desired scope set
            // rather than a delta, so a retry writes the same thing.
            .AddStandardResilienceHandler();

        return services;
    }
}
