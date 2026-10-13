using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Hosting.Azure;
using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;

var builder = Host.CreateApplicationBuilder(args);
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    builder.Services.AddRefTestManagementMigrationServices(builder.Configuration);
    using var migrationHost = builder.Build();
    await migrationHost.Services.MigrateRefTestManagementDatabase();
    return;
}

var hostingMode = HostingModeConfiguration.GetWorkerMode(builder.Configuration);
var hostingProvider = HostingProviderConfiguration.Create(
    builder.Configuration,
    builder.Environment,
    [
        new AzureHostingProviderAdapter()
    ]);
var dataProtectionKeyProtection = hostingProvider.CreateDataProtectionKeyProtection();
builder.Services.AddRefTestManagementHosting(
    builder.Configuration,
    hostingMode,
    dataProtectionKeyProtection);
builder.Services.AddIhfRulesQuestionsHttpClient(builder.Configuration["RulesQuestions:Url"]);
await hostingProvider.ConfigureRedisConnectionAsync(builder.Services);

await builder.Build().RunAsync();
