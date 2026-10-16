namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Finds the users of the configured identity provider who hold an application permission.
/// </summary>
public interface IIdentityUserDirectory
{
    /// <summary>
    /// Returns all users that hold the given permission, either directly or through a role or group assignment.
    /// Implementations must throw rather than return a partial list when the provider cannot be read completely.
    /// </summary>
    Task<IReadOnlyList<IdentityUser>> GetUsersWithPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default);
}
