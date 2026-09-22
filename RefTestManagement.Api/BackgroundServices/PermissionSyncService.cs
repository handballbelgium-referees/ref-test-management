using Handball.Belgium.RefTestManagement.Auth0.Services;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Runs once on startup to ensure all application permissions are registered
/// on the Auth0 API resource server. Missing permissions are added; existing
/// ones are left untouched (additive-only, non-destructive).
/// </summary>
/// <remarks>
/// This is necessary to ensure that permissions are available for assignment to users and roles in Auth0,
/// and that the application can enforce them. Sync failures are logged but do not prevent the application from starting, to avoid downtime due to transient issues with the Auth0 Management API.
/// </remarks>
public sealed class PermissionSyncService(
    IAuth0ManagementService auth0ManagementService,
    ILogger<PermissionSyncService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            await auth0ManagementService.SyncPermissionsAsync(
                [..Permissions.All, Permissions.Superadmin],
                stoppingToken);
            logger.LogInformation("Auth0 permission sync completed successfully");
        }
        catch (Exception ex)
        {
            // Sync failure must not prevent the application from starting
            logger.LogWarning(ex,
                "Auth0 permission sync failed — application will continue without syncing permissions");
        }
    }
}