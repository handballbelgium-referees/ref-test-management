using Microsoft.AspNetCore.Authorization;

namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// An authorization requirement that is satisfied when the user holds <b>any one</b> of the
/// specified permissions (OR semantics). Superadmin and namespace wildcards are still honoured.
/// </summary>
/// <remarks>
/// Policies based on this requirement are created dynamically by
/// <see cref="TaskAuthorizationPolicyProvider"/> using the naming convention
/// <c>anyof:perm1|perm2|perm3</c>.
/// </remarks>
public sealed class AnyTaskPermissionRequirement(params string[] permissions) : IAuthorizationRequirement
{
    public string[] Permissions { get; } = permissions;
}
