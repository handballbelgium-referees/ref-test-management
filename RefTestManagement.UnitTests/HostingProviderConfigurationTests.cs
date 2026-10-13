using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class HostingProviderConfigurationTests
{
    [Theory]
    [InlineData(null, "Local")]
    [InlineData("", "Local")]
    [InlineData("Local", "Local")]
    [InlineData("aZure", "aZure")]
    public void GetProviderReturnsConfiguredProviderName(string? value, string expected)
    {
        var configuration = CreateConfiguration(("Hosting:Provider", value));

        Assert.Equal(expected, HostingProviderConfiguration.GetProvider(configuration));
    }

    [Fact]
    public void CreateRejectsUnregisteredProviders()
    {
        var configuration = CreateConfiguration(("Hosting:Provider", "Unknown"));

        Assert.Throws<InvalidOperationException>(
            () => CreateHostingProvider(configuration));
    }

    [Fact]
    public async Task PasswordRedisUsesTheSharedHostingRegistration()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Local"),
            ("RedisConfiguration:AuthenticationMode", "Password"),
            ("RedisConfiguration:Endpoint", "rediss://:password@redis.example:10000"),
            ("State:Mode", "Shared"));
        var services = new ServiceCollection();

        var hostingProvider = CreateHostingProvider(configuration);
        await hostingProvider.ConfigureRedisConnectionAsync(services);

        Assert.Empty(services);
    }

    [Fact]
    public async Task TokenRedisRequiresTheAzureProvider()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Local"),
            ("State:Mode", "Shared"),
            ("RedisConfiguration:AuthenticationMode", "Token"),
            ("RedisConfiguration:Endpoint", "rediss://redis.example:10000"));

        var hostingProvider = CreateHostingProvider(configuration);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => hostingProvider.ConfigureRedisConnectionAsync(new ServiceCollection()));

        Assert.Contains("Hosting:Provider=Local", exception.Message);
    }

    [Fact]
    public async Task AzureAdapterConfiguresTokenRedis()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Azure"),
            ("State:Mode", "Shared"),
            ("RedisConfiguration:AuthenticationMode", "Token"),
            ("RedisConfiguration:Endpoint", "rediss://redis.example:10000"));
        var hostingProvider = CreateHostingProvider(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => hostingProvider.ConfigureRedisConnectionAsync(new ServiceCollection()));

        Assert.Contains("ManagedIdentityClientId", exception.Message);
    }

    [Fact]
    public void AzureAdapterCreatesKeyVaultDataProtectionConfiguration()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Azure"),
            ("Azure:KeyVault:DataProtectionKeyIdentifier", "https://vault.example/keys/data-protection"));
        var hostingProvider = CreateHostingProvider(configuration);

        Assert.NotNull(hostingProvider.CreateDataProtectionKeyProtection());
    }

    private static HostingProviderConfiguration CreateHostingProvider(IConfiguration configuration) =>
        HostingProviderConfiguration.Create(
            configuration,
            Host.CreateApplicationBuilder().Environment,
            [
                new AzureHostingProviderAdapter()
            ]);

    private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
}
