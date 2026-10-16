using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Requires the permission to be confirmed by the identity provider at request time, in addition to the cached claims.
/// </summary>
public sealed record LivePermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Confirms a <see cref="LivePermissionRequirement"/> against the identity provider.
/// Runs only after the cached claims already grant the permission, so a revoked-in-cache user is rejected without an IdP call.
/// </summary>
internal sealed class LivePermissionHandler(ILivePermissionVerifier verifier)
    : AuthorizationHandler<LivePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LivePermissionRequirement requirement)
    {
        var cached = context.User
            .FindAll(TaskPermissionHandler.PermissionClaimType)
            .Select(c => c.Value);

        if (!PermissionMatcher.IsSatisfied(cached, requirement.Permission))
            return;

        if (await verifier.HasPermissionAsync(context.User, requirement.Permission))
            context.Succeed(requirement);
    }
}
