using HotChocolate.Execution.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.AuditLog;

public static class AuditLogExtensions
{
    /// <summary>
    /// Registers <see cref="AuditLogContext"/>, <see cref="IAuditLogService"/>,
    /// <see cref="IAuditLogContext"/>, and an <c>IHostedService</c> that auto-migrates
    /// the audit log database on startup.
    /// Call this on <see cref="IServiceCollection"/> before building the app.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="connectionString">SQL Server connection string for the audit log database.</param>
    public static IServiceCollection AddAuditLogServices(
        this IServiceCollection services,
        string? connectionString)
    {
        services.AddDbContextFactory<AuditLogContext>(options =>
            options.UseSqlServer(connectionString,
                x => x
                    .EnableRetryOnFailure()
                    .MigrationsAssembly(typeof(AuditLogContext).Assembly.GetName().Name)));
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuditLogContext>(sp => sp.GetRequiredService<AuditLogContext>());
        services.AddHostedService<AuditLogMigrationService>();
        return services;
    }

    /// <summary>
    /// Adds the <c>auditLogs</c> GraphQL query field and the <see cref="AuditLogTypeInterceptor"/>
    /// to the schema builder. Call this inside the <c>AddGraphQLServer()</c> chain.
    /// </summary>
    public static IRequestExecutorBuilder AddAuditLogSchema(
        this IRequestExecutorBuilder builder)
        => builder
            .AddAuditLog()
            .ConfigureSchema(b => b.TryAddTypeInterceptor(typeof(AuditLogTypeInterceptor)));
}
