using Handball.Belgium.RefTestManagement.Api.Identity;
using Microsoft.Extensions.Configuration;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class IdentityProviderSelectorTests
{
    [Fact]
    public void ResolveDefaultsToAuth0WhenProviderIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Equal(IdentityProvider.Auth0, IdentityProviderSelector.Resolve(configuration));
    }

    [Fact]
    public void ResolveReadsKeycloakCaseInsensitively()
    {
        var configuration = Build(new Dictionary<string, string?> { ["Identity:Provider"] = "keycloak" });

        Assert.Equal(IdentityProvider.Keycloak, IdentityProviderSelector.Resolve(configuration));
    }

    [Fact]
    public void ResolveRejectsUnknownProvider()
    {
        var configuration = Build(new Dictionary<string, string?> { ["Identity:Provider"] = "Okta" });

        Assert.Throws<InvalidOperationException>(() => IdentityProviderSelector.Resolve(configuration));
    }

    [Fact]
    public void ValidateSettingsIgnoresAuth0()
    {
        var configuration = new ConfigurationBuilder().Build();

        IdentityProviderSelector.ValidateSettings(configuration, IdentityProvider.Auth0);
    }

    [Fact]
    public void ValidateSettingsAcceptsCompleteKeycloakSettings()
    {
        var configuration = Build(CompleteKeycloakSettings());

        IdentityProviderSelector.ValidateSettings(configuration, IdentityProvider.Keycloak);
    }

    [Theory]
    [InlineData("Authority")]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    [InlineData("Audience")]
    public void ValidateSettingsNamesEachMissingKeycloakSetting(string key)
    {
        var settings = CompleteKeycloakSettings();
        settings[$"Identity:Keycloak:{key}"] = "  ";
        var configuration = Build(settings);

        var exception = Assert.Throws<InvalidOperationException>(
            () => IdentityProviderSelector.ValidateSettings(configuration, IdentityProvider.Keycloak));

        Assert.Contains($"Identity:Keycloak:{key}", exception.Message, StringComparison.Ordinal);
    }

    private static Dictionary<string, string?> CompleteKeycloakSettings() => new()
    {
        ["Identity:Provider"] = "Keycloak",
        ["Identity:Keycloak:Authority"] = "https://login.referees-handballbelgium.be/realms/referees-handballbelgium-beta",
        ["Identity:Keycloak:ClientId"] = "reftest-beta",
        ["Identity:Keycloak:ClientSecret"] = "test-secret",
        ["Identity:Keycloak:Audience"] = "reftest-api",
    };

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
