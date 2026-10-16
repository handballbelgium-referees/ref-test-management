using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class DataProtectionKeySharingTests
{
    [Fact]
    public async Task SeparateServiceProvidersCanUnprotectDataWithDatabaseBackedKeys()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<RefTestManagementContext>()
            .UseSqlite(connection)
            .Options;
        await using (var context = new RefTestManagementContext(options))
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        using var apiProvider = CreateProvider(connection);
        var protector = apiProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("sharing-test");
        var protectedValue = protector.Protect("shared test payload");

        using var workerProvider = CreateProvider(connection);
        var workerProtector = workerProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("sharing-test");

        Assert.Equal("shared test payload", workerProtector.Unprotect(protectedValue));

        static ServiceProvider CreateProvider(SqliteConnection sharedConnection)
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<RefTestManagementContext>(
                builder => builder.UseSqlite(sharedConnection));
            services.AddDataProtection()
                .SetApplicationName("RefTestManagement")
                .PersistKeysToDbContext<RefTestManagementContext>();
            return services.BuildServiceProvider();
        }
    }
}
