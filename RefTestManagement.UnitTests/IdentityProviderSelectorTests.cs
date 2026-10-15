using Handball.Belgium.RefTestManagement.Api.Identity;
using Handball.Belgium.RefTestManagement.Security;
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

    [Theory]
    [InlineData(" Keycloak ")]
    [InlineData("KEYCLOAK")]
    public void ResolveTrimsWhitespaceAndMatchesFactoryRule(string value)
    {
        var configuration = Build(new Dictionary<string, string?> { ["Identity:Provider"] = value });

        Assert.Equal(IdentityProvider.Keycloak, IdentityProviderSelector.Resolve(configuration));
        Assert.Equal("Keycloak", IdentityProviderName.Parse(value));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void ResolveTreatsBlankAsAuth0(string value)
    {
        var configuration = Build(new Dictionary<string, string?> { ["Identity:Provider"] = value });

        Assert.Equal(IdentityProvider.Auth0, IdentityProviderSelector.Resolve(configuration));
        Assert.Equal("Auth0", IdentityProviderName.Parse(value));
    }

    [Theory]
    [InlineData("Keycloak2")]
    [InlineData("Key cloak")]
    public void SharedParserRejectsUnknownValuesLikeTheSelector(string value)
    {
        var configuration = Build(new Dictionary<string, string?> { ["Identity:Provider"] = value });

        Assert.Throws<InvalidOperationException>(() => IdentityProviderName.Parse(value));
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

    [Theory]
    [InlineData(IdentityProvider.Auth0, true)]
    [InlineData(IdentityProvider.Keycloak, false)]
    public void SendsAudienceOnAuthorizeOnlyForAuth0(IdentityProvider provider, bool expected)
    {
        Assert.Equal(expected, IdentityProviderSelector.SendsAudienceOnAuthorize(provider));
    }

    [Theory]
    [InlineData(IdentityProvider.Auth0, "returnTo")]
    [InlineData(IdentityProvider.Keycloak, "post_logout_redirect_uri")]
    public void LogoutReturnParameterMatchesProvider(IdentityProvider provider, string expected)
    {
        Assert.Equal(expected, IdentityProviderSelector.LogoutReturnParameter(provider));
    }

    [Fact]
    public void LogoutUriUsesAuth0EndpointForAuth0()
    {
        var uri = IdentityProviderSelector.LogoutUri("https://tenant.eu.auth0.com", "client", IdentityProvider.Auth0);

        Assert.Equal("https://tenant.eu.auth0.com/v2/logout?client_id=client", uri);
    }

    [Fact]
    public void LogoutUriUsesRealmEndpointForKeycloak()
    {
        var uri = IdentityProviderSelector.LogoutUri(
            "https://login.referees-handballbelgium.be/realms/referees-handballbelgium-beta",
            "reftest-beta",
            IdentityProvider.Keycloak);

        Assert.Equal(
            "https://login.referees-handballbelgium.be/realms/referees-handballbelgium-beta/protocol/openid-connect/logout?client_id=reftest-beta",
            uri);
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
