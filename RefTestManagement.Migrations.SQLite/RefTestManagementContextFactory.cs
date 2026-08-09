using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Handball.Belgium.RefTestManagement.Migrations.SQLite;

public class RefTestManagementContextFactory : IDesignTimeDbContextFactory<RefTestManagementContext>
{
    public RefTestManagementContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RefTestManagementContext>()
            .UseSqlite(
                "Data Source=RefTestManagement.db",
                x => x.MigrationsAssembly("RefTestManagement.Migrations.SQLite"))
            .Options;
        return new RefTestManagementContext(options);
    }
}
