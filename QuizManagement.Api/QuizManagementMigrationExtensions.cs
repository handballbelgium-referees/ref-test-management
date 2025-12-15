using Microsoft.EntityFrameworkCore;
using QuizManagement.Infrastructure;

namespace QuizManagement.Api;

public static class QuizManagementMigrationExtensions
{
    /// <summary>
    /// Applies QuizManagement database migrations on application startup
    /// </summary>
    public static async Task MigrateQuizManagementDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<QuizManagementContext>>().CreateDbContextAsync();

        if (await context.Database.CanConnectAsync())
        {
            await context.Database.MigrateAsync();
        }
    }
}