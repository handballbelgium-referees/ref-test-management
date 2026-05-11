namespace Handball.Belgium.RefTestManagement.Auth0;

/// <summary>
/// Configuration for the Auth0 Management API M2M application.
/// The Domain and Audience come from the shared Auth0 config section.
/// ManagementClientId/Secret are the credentials of a separate M2M application
/// with the "Auth0 Management API" audience granted in the Auth0 dashboard.
/// </summary>
public class Auth0ManagementConfiguration
{
    /// <summary>Auth0 tenant domain, e.g. "your-tenant.eu.auth0.com"</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Identifier of the API registered in Auth0 (used for permission sync)</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Client ID of the M2M application used to call the Management API</summary>
    public string ManagementClientId { get; set; } = string.Empty;

    /// <summary>Client secret of the M2M application</summary>
    public string ManagementClientSecret { get; set; } = string.Empty;
}
