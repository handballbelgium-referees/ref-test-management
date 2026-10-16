using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Migrations.MySQL;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddMySqlDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("RefTestManagement");
        return services.AddRefTestDatabase((_, options) =>
            options.UseMySQL(connStr!,
                x => x.MigrationsAssembly("RefTestManagement.Migrations.MySQL")));
    }
}
