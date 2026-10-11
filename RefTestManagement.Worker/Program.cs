using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Hosting.Azure;
using Handball.Belgium.RefTestManagement.Hosting.Cloudflare;

var builder = Host.CreateApplicationBuilder(args);
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    builder.Services.AddRefTestManagementMigrationServices(builder.Configuration);
    using var migrationHost = builder.Build();
    await migrationHost.Services.MigrateRefTestManagementDatabase();
    return;
}

var hostingMode = HostingModeConfiguration.GetWorkerMode(builder.Configuration);
var dataProtectionKeyProtection =
    CloudflareDataProtectionConfiguration.CreateKeyProtection(builder.Configuration) ??
    AzureDataProtectionConfiguration.CreateKeyProtection(builder.Configuration, builder.Environment);
builder.Services.AddRefTestManagementHosting(
    builder.Configuration,
    hostingMode,
    dataProtectionKeyProtection);
await AzureRedisConfiguration.ConfigureRedisConnectionAsync(builder.Services, builder.Configuration);

await builder.Build().RunAsync();
