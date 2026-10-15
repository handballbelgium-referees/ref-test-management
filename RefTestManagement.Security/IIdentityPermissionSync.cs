namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Pushes application permissions to the configured identity provider. Implementations are additive only.
/// </summary>
public interface IIdentityPermissionSync
{
    /// <summary>
    /// Gets a value indicating whether the provider has the settings needed to run a sync.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Creates any of the given permissions the provider does not yet have. Existing permissions are left unchanged.
    /// </summary>
    Task SyncPermissionsAsync(
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default);
}
