namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Decides whether a set of granted permissions satisfies a required permission.
///
/// Resolution order:
/// 1. <c>superadmin</c> permission → unconditional success.
/// 2. Exact match on the required permission.
/// 3. Namespace wildcard match: e.g. <c>ref-tests:*</c> satisfies <c>ref-tests:create</c>.
/// </summary>
internal static class PermissionMatcher
{
    internal static bool IsSatisfied(IEnumerable<string> grantedPermissions, string requiredPermission)
    {
        var permissions = grantedPermissions as ISet<string>
            ?? grantedPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (permissions.Count == 0)
            return false;

        if (permissions.Contains(Permissions.Superadmin))
            return true;

        if (permissions.Contains(requiredPermission))
            return true;

        var colonIndex = requiredPermission.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        var ns = requiredPermission[..colonIndex];
        return permissions.Contains($"{ns}:*");
    }
}
