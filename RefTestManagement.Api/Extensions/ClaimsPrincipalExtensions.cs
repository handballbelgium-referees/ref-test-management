using System.Security.Claims;

namespace Handball.Belgium.RefTestManagement.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extension methods for <see cref="ClaimsPrincipal"/> to simplify retrieval of common claim values like display name and email.
    /// </summary>
    /// <param name="principal"></param>
    extension(ClaimsPrincipal? principal)
    {
        /// <summary>
        /// Returns the display name for the principal, falling back to email, then "Unknown".
        /// Checks both JWT claim names (e.g. "name") and the standard <see cref="ClaimTypes"/> equivalents.
        /// </summary>
        public string GetDisplayName()
        {
            return principal?.FindFirst("name")?.Value
                   ?? principal?.FindFirst(ClaimTypes.Name)?.Value
                   ?? principal?.FindFirst("email")?.Value
                   ?? principal?.FindFirst(ClaimTypes.Email)?.Value
                   ?? "Unknown";
        }

        /// <summary>
        /// Returns the email address for the principal, or an empty string if not found.
        /// Checks both JWT claim names (e.g. "email") and the standard <see cref="ClaimTypes"/> equivalent.
        /// </summary>
        public string GetEmail()
        {
            return principal?.FindFirst("email")?.Value
                   ?? principal?.FindFirst(ClaimTypes.Email)?.Value
                   ?? string.Empty;
        }
        
        public HashSet<string> GetPermissions()
        {
            return principal?.FindAll("permissions")
                .Select(c => c.Value)
                .ToHashSet() ?? [];
        }
    }
}
