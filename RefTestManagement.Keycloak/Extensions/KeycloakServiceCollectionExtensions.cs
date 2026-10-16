using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Keycloak;

public static class KeycloakServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Keycloak permission sync. Provider selection is the host's responsibility.
    /// </summary>
    public static IServiceCollection AddKeycloakPermissionSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KeycloakAdminConfiguration>(configuration.GetSection("Identity:Keycloak"));

        services.AddHttpClient<KeycloakPermissionSyncService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .RemoveAllLoggers()
            .AddStandardResilienceHandler();

        return services;
    }
}
