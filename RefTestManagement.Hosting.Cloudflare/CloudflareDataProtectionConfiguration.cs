using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Handball.Belgium.RefTestManagement.Hosting.Cloudflare;

public static class CloudflareDataProtectionConfiguration
{
    private const string ProviderName = "Cloudflare";
    private const string PfxConfigurationKey = "Cloudflare:DataProtection:PfxBase64";
    private const string PasswordConfigurationKey = "Cloudflare:DataProtection:PfxPassword";

    public static Action<IDataProtectionBuilder>? CreateKeyProtection(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!string.Equals(configuration["Hosting:Provider"], ProviderName, StringComparison.OrdinalIgnoreCase))
            return null;

        var pfxBase64 = configuration[PfxConfigurationKey];
        var pfxPassword = configuration[PasswordConfigurationKey];
        if (string.IsNullOrWhiteSpace(pfxBase64) || string.IsNullOrEmpty(pfxPassword))
        {
            throw new InvalidOperationException(
                $"Hosting:Provider={ProviderName} requires {PfxConfigurationKey} and {PasswordConfigurationKey}.");
        }

        byte[] pfxBytes;
        try
        {
            pfxBytes = Convert.FromBase64String(pfxBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"{PfxConfigurationKey} must contain base64-encoded PKCS#12 data.",
                exception);
        }

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(
                pfxBytes,
                pfxPassword,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfxBytes);
        }

        using var privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is null || privateKey.KeySize < 2048)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"{PfxConfigurationKey} must contain an RSA private key of at least 2048 bits.");
        }

        return builder => builder.ProtectKeysWithCertificate(certificate);
    }
}
