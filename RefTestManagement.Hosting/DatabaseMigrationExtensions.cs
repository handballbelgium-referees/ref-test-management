using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Hosting;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateRefTestManagementDatabase(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<RefTestManagementContext>>()
            .CreateDbContextAsync(cancellationToken);

        await context.Database.MigrateAsync(cancellationToken);
    }
}