using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Permissions;

public static class PermissionPolicies
{
    /// <summary>
    /// Registers an authorization policy for every <see cref="Permission"/> constant,
    /// each requiring the matching value in the <c>permissions</c> claim.
    /// </summary>
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in All)
            builder.AddPolicy(permission, p => p.RequireClaim("permissions", permission));

        return builder;
    }

    /// <summary>All permission strings defined in <see cref="Permission"/>.</summary>
    private static IReadOnlyList<string> All { get; } =
    [
        Permission.RefTests.Read,
        Permission.RefTests.Create,
        Permission.RefTests.Delete,
        Permission.RefTests.UpdateDetails,
        Permission.RefTests.UpdateConfiguration,
        Permission.RefTests.ExtendTime,
        Permission.RefTests.UpdateNotifications,
        Permission.RefTests.RegenerateToken,
        Permission.RefTests.Reset,
        Permission.RefTests.Revive,
        Permission.RefTests.SendInvitations,
        Permission.RefTests.SendResults,
        Permission.RefTests.SendReport,
        Permission.RefTests.Approve,
        Permission.RefTests.ReadAuditLog,
    ];
}
