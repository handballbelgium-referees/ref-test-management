using Handball.Belgium.RefTestManagement.Application.RefTests.Creation;
using Handball.Belgium.RefTestManagement.Application.RefTests.Deletion;
using Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RefTestCreationHandler>();
        services.AddScoped<RefTestDeletionHandler>();
        services.AddScoped<RefTestLifecycleHandler>();
        return services;
    }
}
