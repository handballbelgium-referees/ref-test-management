using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Handball.Belgium.RefTestManagement.Migrations.MySQL;

public class RefTestManagementContextFactory : IDesignTimeDbContextFactory<RefTestManagementContext>
{
    public RefTestManagementContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RefTestManagementContext>()
            .UseMySQL(
                "Server=localhost;Database=RefTestManagement;User=root;Password=root;",
                x => x.MigrationsAssembly("RefTestManagement.Migrations.MySQL"))
            .Options;
        return new RefTestManagementContext(options);
    }
}
