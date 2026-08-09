using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Migrations.SqlServer;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddSqlServerDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("RefTestManagement");
        return services.AddRefTestDatabase((_, options) =>
            options.UseSqlServer(connStr, x => x
                .EnableRetryOnFailure()
                .MigrationsAssembly("RefTestManagement.Migrations.SqlServer")));
    }
}
