using HotChocolate.Execution.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

public static class AuditLogExtensions
{
    /// <summary>
    /// Applies the <see cref="AuditLog"/> EF Core entity configuration to the model.
    /// Call this from <c>OnModelCreating</c> on any <c>DbContext</c> that implements
    /// <see cref="IAuditLogContext"/>.
    /// </summary>
    public static ModelBuilder ApplyAuditLogConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        return modelBuilder;
    }

    /// <summary>
    /// Registers the <see cref="IAuditLogService"/> implementation and <see cref="IAuditLogContext"/>
    /// in the DI container. Call this on <see cref="IServiceCollection"/> before building the app.
    /// </summary>
    public static IServiceCollection AddAuditLogServices<TContext>(
        this IServiceCollection services)
        where TContext : DbContext, IAuditLogContext
    {
        services.AddScoped<IAuditLogService, AuditLogService<TContext>>();
        services.AddScoped<IAuditLogContext>(sp => sp.GetRequiredService<TContext>());
        return services;
    }

    /// <summary>
    /// Adds the <c>auditLogs</c> GraphQL query field to the given schema builder.
    /// Call this on whichever <c>AddGraphQLServer()</c> builder should expose the query.
    /// </summary>
    public static IRequestExecutorBuilder AddAuditLogQueries(
        this IRequestExecutorBuilder builder)
        => builder
            .AddAuditLog()
            .ConfigureSchema(b => b.TryAddTypeInterceptor(typeof(AuditLogTypeInterceptor)));
}
