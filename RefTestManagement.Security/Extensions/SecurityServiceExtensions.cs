using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Security;

public static class SecurityServiceExtensions
{
    /// <summary>
    /// Registers the task-based permission handler and creates an authorization policy
    /// for every permission defined in <see cref="Permissions"/>.
    /// Also registers <see cref="TaskAuthorizationPolicyProvider"/> for dynamic
    /// <c>anyof:</c> OR-permission policies.
    /// </summary>
    public static IServiceCollection AddTaskBasedAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, TaskPermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, AnyTaskPermissionHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, TaskAuthorizationPolicyProvider>();

        var builder = services.AddAuthorizationBuilder();
        foreach (var permission in Permissions.All)
        {
            var captured = permission;
            builder.AddPolicy(captured, policy =>
                policy.AddRequirements(new TaskPermissionRequirement(captured)));
        }

        return services;
    }
}
