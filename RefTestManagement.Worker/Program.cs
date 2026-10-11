using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Hosting.Azure;

var builder = Host.CreateApplicationBuilder(args);
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    builder.Services.AddRefTestManagementMigrationServices(builder.Configuration);
    using var migrationHost = builder.Build();
    await migrationHost.Services.MigrateRefTestManagementDatabase();
    return;
}

var hostingMode = HostingModeConfiguration.GetWorkerMode(builder.Configuration);
builder.Services.AddRefTestManagementHosting(
    builder.Configuration,
    hostingMode,
    AzureDataProtectionConfiguration.CreateKeyProtection(builder.Configuration, builder.Environment));

await builder.Build().RunAsync();
