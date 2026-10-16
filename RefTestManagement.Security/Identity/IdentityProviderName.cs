namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// The single rule for reading the <c>Identity:Provider</c> setting. Shared by the API and the hosting
/// layer so both select the same provider for the same configuration value.
/// </summary>
public static class IdentityProviderName
{
    public const string Auth0 = "Auth0";
    public const string Keycloak = "Keycloak";

    /// <summary>
    /// Returns the canonical provider name. A missing or blank value selects Auth0 so existing deployments
    /// keep working. Any other unrecognized value throws rather than silently falling back to a provider.
    /// </summary>
    public static string Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Auth0;

        var trimmed = value.Trim();
        if (string.Equals(trimmed, Auth0, StringComparison.OrdinalIgnoreCase))
            return Auth0;
        if (string.Equals(trimmed, Keycloak, StringComparison.OrdinalIgnoreCase))
            return Keycloak;

        throw new InvalidOperationException(
            $"Unsupported Identity:Provider '{value}'. Expected 'Auth0' or 'Keycloak'.");
    }
}
