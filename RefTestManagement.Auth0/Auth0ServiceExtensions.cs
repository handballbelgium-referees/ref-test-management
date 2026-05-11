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

        services.AddHttpClient<IAuth0ManagementService, Auth0ManagementService>();

        return services;
    }
}
