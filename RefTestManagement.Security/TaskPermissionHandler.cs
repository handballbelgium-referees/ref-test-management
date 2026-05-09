using Microsoft.AspNetCore.Authorization;

namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Evaluates <see cref="TaskPermissionRequirement"/> against the authenticated user's permission claims.
///
/// Resolution order:
/// 1. <c>superadmin</c> permission → unconditional success.
/// 2. Exact match on the required permission.
/// 3. Namespace wildcard match: e.g. <c>ref-tests:*</c> satisfies <c>ref-tests:create</c>.
///
/// Permissions are read from the <c>permissions</c> claim, which Auth0 RBAC places in the
/// access token when "Add Permissions in the Access Token" is enabled on the API.
/// For cookie-based sessions the claim is copied from the access token during OIDC token validation.
/// </summary>
public sealed class TaskPermissionHandler : AuthorizationHandler<TaskPermissionRequirement>
{
    private const string PermissionClaimType = "permissions";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TaskPermissionRequirement requirement)
    {
        var permissions = context.User
            .FindAll(PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (permissions.Count == 0)
            return Task.CompletedTask;

        // Superadmin bypasses all checks
        if (permissions.Contains(Permissions.Superadmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Exact permission match
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Namespace wildcard: "ref-tests:*" satisfies any "ref-tests:<action>"
        var colonIndex = requirement.Permission.IndexOf(':');
        if (colonIndex <= 0) return Task.CompletedTask;
        
        var ns = requirement.Permission[..colonIndex];
        if (permissions.Contains($"{ns}:*"))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
