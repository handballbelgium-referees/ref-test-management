using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api;

public static class RefTestManagementMigrationExtensions
{
    /// <summary>
    /// Applies RefTestManagement database migrations on application startup
    /// </summary>
    public static async Task MigrateRefTestManagementDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<RefTestManagementContext>>().CreateDbContextAsync();

        if (await context.Database.CanConnectAsync())
        {
            await context.Database.MigrateAsync();
        }
    }
}