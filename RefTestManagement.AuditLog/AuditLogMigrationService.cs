using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.AuditLog;

/// <summary>
/// Runs <c>Database.MigrateAsync()</c> on <see cref="AuditLogContext"/> at application startup.
/// Registered automatically by <see cref="AuditLogExtensions.AddAuditLog"/>.
/// </summary>
internal sealed class AuditLogMigrationService(IServiceProvider sp) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = sp.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditLogContext>>();
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
