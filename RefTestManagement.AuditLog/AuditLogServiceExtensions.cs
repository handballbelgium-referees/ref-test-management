using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.AuditLog;

public static class AuditLogServiceExtensions
{
    public static AuditLogOptions AddAuditLogging(
        this IServiceCollection services,
        Action<AuditLogOptions>? configure = null)
    {
        var options = new AuditLogOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<AuditSaveChangesInterceptor>();

        return options;
    }
}
