using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Migrations.SQLite;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddSqliteDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("RefTestManagement");
        return services.AddRefTestDatabase((_, options) =>
            options.UseSqlite(connStr,
                x => x.MigrationsAssembly("RefTestManagement.Migrations.SQLite")));
    }
}
