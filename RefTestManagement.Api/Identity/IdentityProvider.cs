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
}
