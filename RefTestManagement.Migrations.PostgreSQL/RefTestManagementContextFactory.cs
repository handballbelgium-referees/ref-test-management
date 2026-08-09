using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Handball.Belgium.RefTestManagement.Migrations.PostgreSQL;

public class RefTestManagementContextFactory : IDesignTimeDbContextFactory<RefTestManagementContext>
{
    public RefTestManagementContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RefTestManagementContext>()
            .UseNpgsql(
                "Host=localhost;Database=RefTestManagement;Username=postgres;Password=postgres",
                x => x.MigrationsAssembly("RefTestManagement.Migrations.PostgreSQL"))
            .Options;
        return new RefTestManagementContext(options);
    }
}
