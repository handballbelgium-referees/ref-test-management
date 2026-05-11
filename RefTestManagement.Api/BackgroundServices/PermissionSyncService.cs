using Handball.Belgium.RefTestManagement.Auth0;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Runs once on startup to ensure all application permissions are registered
/// on the Auth0 API resource server. Missing permissions are added; existing
/// ones are left untouched (additive-only, non-destructive).
/// </summary>
public sealed class PermissionSyncService(
    IAuth0ManagementService auth0ManagementService,
    ILogger<PermissionSyncService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await auth0ManagementService.SyncPermissionsAsync(Permissions.All, cancellationToken);
            logger.LogInformation("Auth0 permission sync completed successfully");
        }
        catch (Exception ex)
        {
            // Sync failure must not prevent the application from starting
            logger.LogWarning(ex, "Auth0 permission sync failed — application will continue without syncing permissions");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
