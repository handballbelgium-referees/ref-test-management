using System.Security.Claims;

namespace Handball.Belgium.RefTestManagement.Permissions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Returns the <c>email</c> claim value, or <c>"unknown"</c> when absent.</summary>
    public static string GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirst("email")?.Value
        ?? principal.FindFirst(ClaimTypes.Email)?.Value
        ?? "unknown";

    /// <summary>Returns the <c>name</c> claim value, or <c>null</c> when absent.</summary>
    public static string? GetName(this ClaimsPrincipal principal) =>
        principal.FindFirst("name")?.Value
        ?? principal.FindFirst(ClaimTypes.Name)?.Value;

    /// <summary>
    /// Returns <c>true</c> when the access token contains the given Auth0 permission scope.
    /// Auth0 places individual scopes as separate <c>permissions</c> claims in the access token.
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal principal, string permission) =>
        principal.HasClaim("permissions", permission);
}
