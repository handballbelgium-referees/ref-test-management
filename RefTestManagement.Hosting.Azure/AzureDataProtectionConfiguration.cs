using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.Hosting.Azure;

public static class AzureDataProtectionConfiguration
{
    public static Action<IDataProtectionBuilder>? CreateKeyProtection(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration["Hosting:Provider"];
        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "A non-development host must configure a Data Protection key-encryption provider.");

            return null;
        }

        if (!string.Equals(provider, "Azure", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Hosting:Provider '{provider}' has no registered Data Protection adapter.");

        var keyIdentifier = configuration["Azure:KeyVault:DataProtectionKeyIdentifier"];
        if (!Uri.TryCreate(keyIdentifier, UriKind.Absolute, out var keyUri) ||
            keyUri.Scheme != Uri.UriSchemeHttps ||
            keyUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) is not ["keys", _])
            throw new InvalidOperationException(
                "Azure:KeyVault:DataProtectionKeyIdentifier must be an HTTPS, versionless Key Vault key identifier.");

        var managedIdentityClientId = configuration["Azure:KeyVault:ManagedIdentityClientId"];
        var managedIdentity = string.IsNullOrWhiteSpace(managedIdentityClientId)
            ? ManagedIdentityId.SystemAssigned
            : ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId);
        TokenCredential credential = new ManagedIdentityCredential(managedIdentity);

        return builder => builder.ProtectKeysWithAzureKeyVault(keyUri, credential);
    }
}
