using System.Net;
using Handball.Belgium.RefTestManagement.Auth0.Configurations;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Runs once on startup to ensure all application permissions are registered
/// on the Auth0 API resource server. Missing permissions are added; existing
/// ones are left untouched (additive-only, non-destructive).
/// </summary>
/// <remarks>
/// This is necessary to ensure that permissions are available for assignment to users and roles in Auth0,
/// and that the application can enforce them. Sync failures do not prevent the application from starting.
/// Transient failures receive at most four retries with 2, 4, 8, and 16 second delays.
/// </remarks>
public sealed class PermissionSyncService(
    IAuth0ManagementService auth0ManagementService,
    IOptions<Auth0ManagementConfiguration> options,
    TimeProvider timeProvider,
    ILogger<PermissionSyncService> logger)
    : BackgroundService
{
    private const int MaximumAttempts = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        stoppingToken.ThrowIfCancellationRequested();

        var configuration = options.Value;
        if (string.IsNullOrWhiteSpace(configuration.Domain) ||
            string.IsNullOrWhiteSpace(configuration.Audience) ||
            string.IsNullOrWhiteSpace(configuration.ManagementClientId) ||
            string.IsNullOrWhiteSpace(configuration.ManagementClientSecret))
        {
            logger.LogWarning(
                "Auth0 permission sync skipped because required configuration is missing");
            return;
        }

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await auth0ManagementService.SyncPermissionsAsync(
                    [..Permissions.All, Permissions.Superadmin],
                    stoppingToken);
                logger.LogInformation("Auth0 permission sync completed successfully");
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                IsTransient(exception) && attempt < MaximumAttempts)
            {
                var delay = GetRetryDelay(attempt);
                logger.LogWarning(
                    "Auth0 permission sync attempt {Attempt} failed; retrying in {DelaySeconds} seconds",
                    attempt,
                    delay.TotalSeconds);
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            catch (Exception)
            {
                // Avoid exception details: Auth0 failures may contain sensitive data.
                logger.LogWarning(
                    "Auth0 permission sync failed after {AttemptCount} attempt(s); application will continue without syncing permissions",
                    attempt);
                return;
            }
        }
    }

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromTicks(InitialRetryDelay.Ticks * (1L << (attempt - 1)));

    private static bool IsTransient(Exception exception)
    {
        if (exception is HttpRequestException requestException)
        {
            var statusCode = requestException.StatusCode;
            return statusCode is null ||
                   statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                   (statusCode.HasValue && (int)statusCode.Value >= 500);
        }

        return exception is TimeoutException
            or OperationCanceledException
            or BrokenCircuitException
            or TimeoutRejectedException;
    }
}
