using Handball.Belgium.RefTestManagement.Migrations.MySQL;
using Handball.Belgium.RefTestManagement.Migrations.PostgreSQL;
using Handball.Belgium.RefTestManagement.Migrations.SqlServer;
using Handball.Belgium.RefTestManagement.Migrations.SQLite;

namespace Handball.Belgium.RefTestManagement.Hosting;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddDatabaseProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return configuration.GetValue<string>("DatabaseProvider") switch
        {
            "SqlServer" or null => services.AddSqlServerDatabase(configuration),
            "PostgreSQL"        => services.AddPostgreSqlDatabase(configuration),
            "SQLite"            => services.AddSqliteDatabase(configuration),
            "MySQL"             => services.AddMySqlDatabase(configuration),
            var provider        => throw new InvalidOperationException(
                $"Unknown DatabaseProvider '{provider}'. Valid values: SqlServer, PostgreSQL, SQLite, MySQL.")
        };
    }
}
