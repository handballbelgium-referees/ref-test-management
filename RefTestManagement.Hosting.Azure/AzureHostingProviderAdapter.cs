using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.Hosting.Azure;

public sealed class AzureHostingProviderAdapter : IHostingProviderAdapter
{
    public string ProviderName => "Azure";

    public bool SupportsRedisTokenAuthentication => true;

    public Action<IDataProtectionBuilder>? CreateDataProtectionKeyProtection(
        IConfiguration configuration,
        IHostEnvironment environment) =>
        AzureDataProtectionConfiguration.CreateKeyProtection(configuration, environment);

    public Task ConfigureRedisConnectionAsync(
        IServiceCollection services,
        IConfiguration configuration) =>
        AzureRedisConfiguration.ConfigureRedisConnectionAsync(services, configuration);
}
