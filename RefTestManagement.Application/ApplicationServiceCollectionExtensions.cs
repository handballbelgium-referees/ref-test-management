using Handball.Belgium.RefTestManagement.Application.RefTests.Creation;
using Handball.Belgium.RefTestManagement.Application.RefTests.Deletion;
using Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;
using Handball.Belgium.RefTestManagement.Application.RefTests.Approval;
using Handball.Belgium.RefTestManagement.Application.RefTests.Reset;
using Handball.Belgium.RefTestManagement.Application.RefTests.Update;
using Handball.Belgium.RefTestManagement.Application.RefTests.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RefTestCreationHandler>();
        services.AddScoped<RefTestDeletionHandler>();
        services.AddScoped<RefTestLifecycleHandler>();
        services.AddScoped<RefTestApprovalHandler>();
        services.AddScoped<RefTestResetHandler>();
        services.AddScoped<RefTestUpdateHandler>();
        services.AddScoped<RefTestEmailHandler>();
        return services;
    }
}
