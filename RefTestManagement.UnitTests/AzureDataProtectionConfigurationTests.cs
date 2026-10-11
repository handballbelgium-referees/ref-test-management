using Handball.Belgium.RefTestManagement.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class AzureDataProtectionConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Local")]
    public void DevelopmentLocalProviderDoesNotRequireAzureConfiguration(string? provider)
    {
        var configuration = CreateConfiguration(("Hosting:Provider", provider));

        Assert.Null(AzureDataProtectionConfiguration.CreateKeyProtection(
            configuration, new TestHostEnvironment(Environments.Development)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Local")]
    public void NonDevelopmentHostMustConfigureKeyEncryption(string? provider)
    {
        var configuration = CreateConfiguration(("Hosting:Provider", provider));

        Assert.Throws<InvalidOperationException>(() =>
            AzureDataProtectionConfiguration.CreateKeyProtection(
                configuration, new TestHostEnvironment(Environments.Production)));
    }

    [Fact]
    public void AzureProviderRequiresAnHttpsVersionlessKeyIdentifier()
    {
        foreach (var invalidIdentifier in new string?[]
                 {
                     null,
                     "http://vault.example/keys/data-protection",
                     "https://vault.example/keys/data-protection/version"
                 })
        {
            var configuration = CreateConfiguration(
                ("Hosting:Provider", "Azure"),
                ("Azure:KeyVault:DataProtectionKeyIdentifier", invalidIdentifier));

            Assert.Throws<InvalidOperationException>(() =>
                AzureDataProtectionConfiguration.CreateKeyProtection(
                    configuration, new TestHostEnvironment(Environments.Production)));
        }
    }

    [Fact]
    public void AzureProviderCreatesAKeyProtectionAdapter()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Azure"),
            ("Azure:KeyVault:DataProtectionKeyIdentifier", "https://vault.example/keys/data-protection"));

        Assert.NotNull(AzureDataProtectionConfiguration.CreateKeyProtection(
            configuration, new TestHostEnvironment(Environments.Production)));
    }

    [Fact]
    public void UnsupportedProviderFailsInsteadOfSkippingKeyProtection()
    {
        var configuration = CreateConfiguration(("Hosting:Provider", "Cloudflare"));

        Assert.Throws<InvalidOperationException>(() =>
            AzureDataProtectionConfiguration.CreateKeyProtection(
                configuration, new TestHostEnvironment(Environments.Development)));
    }

    private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "RefTestManagement";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
