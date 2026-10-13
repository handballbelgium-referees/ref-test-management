using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.Hosting.Cloudflare;

public sealed class CloudflareHostingProviderAdapter : IHostingProviderAdapter
{
    public string ProviderName => "Cloudflare";

    public Action<IDataProtectionBuilder>? CreateDataProtectionKeyProtection(
        IConfiguration configuration,
        IHostEnvironment environment) =>
        CloudflareDataProtectionConfiguration.CreateKeyProtection(configuration);
}
