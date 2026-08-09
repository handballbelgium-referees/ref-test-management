using Handball.Belgium.RefTestManagement.AuditLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Extensions;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddRefTestDatabase(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureProvider)
    {
        return services.AddDbContextFactory<RefTestManagementContext>((sp, options) =>
        {
            configureProvider(sp, options);
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
#if DEBUG
            options.EnableSensitiveDataLogging();
#endif
        });
    }
}
