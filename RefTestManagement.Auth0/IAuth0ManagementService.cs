namespace Handball.Belgium.RefTestManagement.Auth0;

/// <summary>
/// Provides Auth0 Management API operations needed by the application:
/// user discovery by permission and permission synchronisation.
/// </summary>
public interface IAuth0ManagementService
{
    /// <summary>
    /// Returns all Auth0 users that hold the given permission,
    /// either directly or through a role assignment.
    /// </summary>
    Task<IReadOnlyList<Auth0User>> GetUsersWithPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures every permission in <paramref name="permissions"/> exists on the
    /// Auth0 API resource server identified by <see cref="Auth0ManagementConfiguration.Audience"/>.
    /// Missing permissions are added; existing ones are left untouched (additive only).
    /// </summary>
    Task SyncPermissionsAsync(
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default);
}
