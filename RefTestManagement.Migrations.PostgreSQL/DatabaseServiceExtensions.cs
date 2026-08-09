using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Migrations.PostgreSQL;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddPostgreSqlDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("RefTestManagement");
        return services.AddRefTestDatabase((_, options) =>
            options.UseNpgsql(connStr, x => x
                .EnableRetryOnFailure()
                .MigrationsAssembly("RefTestManagement.Migrations.PostgreSQL")));
    }
}
