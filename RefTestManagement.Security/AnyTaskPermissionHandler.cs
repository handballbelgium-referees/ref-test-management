using Microsoft.AspNetCore.Authorization;

namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Handles <see cref="AnyTaskPermissionRequirement"/>: succeeds when the user holds
/// at least one of the required permissions (OR semantics).
/// Superadmin bypasses all checks; namespace wildcards (e.g. <c>ref-tests:*</c>) are respected.
/// </summary>
public sealed class AnyTaskPermissionHandler : AuthorizationHandler<AnyTaskPermissionRequirement>
{
    private const string PermissionClaimType = "permissions";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyTaskPermissionRequirement requirement)
    {
        var userPermissions = context.User
            .FindAll(PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (userPermissions.Count == 0)
            return Task.CompletedTask;

        if (userPermissions.Contains(Permissions.Superadmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        foreach (var permission in requirement.Permissions)
        {
            if (userPermissions.Contains(permission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var colonIndex = permission.IndexOf(':');
            if (colonIndex > 0)
            {
                var ns = permission[..colonIndex];
                if (userPermissions.Contains($"{ns}:*"))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
        }

        return Task.CompletedTask;
    }
}
