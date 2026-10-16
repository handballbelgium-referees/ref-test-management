using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Handball.Belgium.RefTestManagement.Migrations.SqlServer;

public class RefTestManagementContextFactory : IDesignTimeDbContextFactory<RefTestManagementContext>
{
    public RefTestManagementContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RefTestManagementContext>()
            .UseSqlServer(
                "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;",
                x => x.MigrationsAssembly("RefTestManagement.Migrations.SqlServer"))
            .Options;
        return new RefTestManagementContext(options);
    }
}
