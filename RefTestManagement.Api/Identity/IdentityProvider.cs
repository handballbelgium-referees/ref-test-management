namespace Handball.Belgium.RefTestManagement.Api.Identity;

/// <summary>
/// The login provider that issues user identities and permission claims. Only one provider is active per
/// deployment; switching providers starts with an empty user and permission set.
/// </summary>
public enum IdentityProvider
{
    Auth0,
    Keycloak
}

public static class IdentityProviderSelector
{
    public const string ConfigurationKey = "Identity:Provider";

    /// <summary>
    /// Reads <c>Identity:Provider</c>. A missing or empty value selects Auth0 so existing deployments keep working.
    /// Any other unrecognized value fails fast rather than silently falling back to a provider.
    /// </summary>
    public static IdentityProvider Resolve(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
            return IdentityProvider.Auth0;

        if (Enum.TryParse<IdentityProvider>(value.Trim(), ignoreCase: true, out var provider)
            && Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"Unsupported {ConfigurationKey} '{value}'. Expected 'Auth0' or 'Keycloak'.");
    }

    /// <summary>The authentication scheme name used for the OpenID Connect challenge and sign-out.</summary>
    public static string SchemeName(IdentityProvider provider) => provider switch
    {
        IdentityProvider.Keycloak => "Keycloak",
        _ => "Auth0"
    };

    /// <summary>The claims issuer stamped on permission claims, matching the scheme name.</summary>
    public static string ClaimsIssuer(IdentityProvider provider) => SchemeName(provider);

    /// <summary>
    /// The OpenID Connect authority. Auth0 derives it from its tenant domain; Keycloak uses the realm URL
    /// configured in <c>Identity:Keycloak:Authority</c>.
    /// </summary>
    public static string Authority(IConfiguration configuration, IdentityProvider provider) => provider switch
    {
        IdentityProvider.Keycloak => configuration[$"{SettingsPath(provider)}:Authority"] ?? string.Empty,
        _ => $"https://{configuration[$"{SettingsPath(provider)}:Domain"]}"
    };

    /// <summary>The configuration path, under <c>Identity</c>, that holds the provider's client settings.</summary>
    public static string SettingsPath(IdentityProvider provider) => $"Identity:{SchemeName(provider)}";

    /// <summary>
    /// Whether the authorize request must carry the API audience. Keycloak adds it through a client mapper on the
    /// login client instead.
    /// </summary>
    public static bool SendsAudienceOnAuthorize(IdentityProvider provider) => provider != IdentityProvider.Keycloak;

    /// <summary>The query parameter that carries the post-logout redirect target.</summary>
    public static string LogoutReturnParameter(IdentityProvider provider) => provider == IdentityProvider.Keycloak
        ? "post_logout_redirect_uri"
        : "returnTo";

    /// <summary>
    /// The provider logout endpoint. Keycloak uses the realm OIDC logout endpoint; Auth0 uses <c>/v2/logout</c>.
    /// </summary>
    public static string LogoutUri(string authority, string clientId, IdentityProvider provider) =>
        provider == IdentityProvider.Keycloak
            ? $"{authority}/protocol/openid-connect/logout?client_id={clientId}"
            : $"{authority}/v2/logout?client_id={clientId}";

    /// <summary>
    /// Fails fast when a Keycloak deployment lacks settings needed for the confidential OpenID Connect login
    /// and the API audience check. Auth0 validation is unchanged.
    /// </summary>
    public static void ValidateSettings(IConfiguration configuration, IdentityProvider provider)
    {
        if (provider != IdentityProvider.Keycloak)
            return;

        var section = SettingsPath(provider);
        var missing = new[] { "Authority", "ClientId", "ClientSecret", "Audience" }
            .Where(key => string.IsNullOrWhiteSpace(configuration[$"{section}:{key}"]))
            .Select(key => $"{section}:{key}")
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Identity provider 'Keycloak' is selected but these settings are missing: {string.Join(", ", missing)}.");
        }
    }
}
