using Handball.Belgium.RefTestManagement.Auth0.Models;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Auth0.Services;

/// <summary>
/// Provides Auth0 Management API operations needed by the application:
/// user discovery by permission and permission synchronisation.
/// Permission synchronisation and per-user permission reads are inherited from
/// <see cref="IIdentityPermissionSync"/>.
/// </summary>
public interface IAuth0ManagementService : IIdentityPermissionSync
{
    /// <summary>
    /// Returns all Auth0 users that hold the given permission,
    /// either directly or through a role assignment.
    /// </summary>
    Task<IReadOnlyList<Auth0User>> GetUsersWithPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default);
}
