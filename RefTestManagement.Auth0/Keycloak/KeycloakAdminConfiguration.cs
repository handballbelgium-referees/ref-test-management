namespace Handball.Belgium.RefTestManagement.Auth0.Keycloak;

/// <summary>
/// Settings for the Keycloak Admin REST API used to synchronise permissions.
/// Bound from the <c>Identity:Keycloak</c> configuration section.
/// </summary>
public sealed class KeycloakAdminConfiguration
{
    /// <summary>Realm issuer, for example <c>https://login.example.com/realms/my-realm</c>.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Client ID of the API client that holds the permission roles (the audience).</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Client ID of the service account used for administration.</summary>
    public string AdminClientId { get; set; } = string.Empty;

    /// <summary>Client secret of the service account used for administration.</summary>
    public string AdminClientSecret { get; set; } = string.Empty;
}
