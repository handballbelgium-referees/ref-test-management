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
/// Permissions are read from the <c>permissions</c> claim after the API refreshes the user's
/// effective grants from Auth0 Management API. Cookie claims are refreshed before authorization.
/// </summary>
public sealed class TaskPermissionHandler : AuthorizationHandler<TaskPermissionRequirement>
{
    internal const string PermissionClaimType = "permissions";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TaskPermissionRequirement requirement)
    {
        var permissions = context.User
            .FindAll(PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (PermissionMatcher.IsSatisfied(permissions, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
