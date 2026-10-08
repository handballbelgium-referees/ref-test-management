using System.Collections.Frozen;
using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>
/// Resolves a user's current effective permissions from Auth0.
/// </summary>
public interface IPermissionSnapshotService
{
    /// <summary>
    /// Returns a fresh permission snapshot, or <see langword="null"/> if the user cannot be
    /// identified or a fresh snapshot could not be obtained.
    /// </summary>
    Task<IReadOnlySet<string>?> GetCurrentPermissionsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Caches effective Auth0 permissions briefly and shares concurrent refreshes per user.
/// </summary>
public sealed class PermissionSnapshotService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PermissionSnapshotService> logger)
    : IPermissionSnapshotService
{
    internal const int MaximumConcurrentRefreshes = 4;
    // Leaves room for 60 queued subjects beyond the active calls; each refresh expires within 30 seconds.
    internal const int MaximumPendingRefreshes = 64;
    // One size unit per subject keeps this cache bounded independently of other application caches.
    internal const int MaximumCachedSnapshots = 1_024;

    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(30);
    // Includes semaphore queue time and the complete multi-request Auth0 lookup.
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(30);
    private const string CacheKeyPrefix = "Auth0PermissionSnapshot:";

    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = MaximumCachedSnapshots
    });
    private readonly object _inFlightLock = new();
    private readonly Dictionary<string, TaskCompletionSource<IReadOnlySet<string>?>> _inFlight =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refreshLimit = new(MaximumConcurrentRefreshes);

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>?> GetCurrentPermissionsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.Identities.Any(identity => identity.IsAuthenticated))
            return null;

        var userId = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            PermissionSnapshotLogger.RefreshFailed(logger, "MissingSubject", null, null);
            return null;
        }

        Task<IReadOnlySet<string>?>? refreshTask = null;
        TaskCompletionSource<IReadOnlySet<string>?>? refresh = null;
        lock (_inFlightLock)
        {
            if (TryGetCachedPermissions(userId, timeProvider.GetUtcNow(), out var cachedPermissions))
                return cachedPermissions;

            if (_inFlight.TryGetValue(userId, out var inFlight))
            {
                refreshTask = inFlight.Task;
            }
            else if (_inFlight.Count < MaximumPendingRefreshes)
            {
                refresh = new TaskCompletionSource<IReadOnlySet<string>?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _inFlight.Add(userId, refresh);
                refreshTask = refresh.Task;
            }
        }

        if (refreshTask is null)
        {
            PermissionSnapshotLogger.RefreshFailed(logger, "Capacity", null, null);
            return null;
        }

        if (refresh is not null)
            _ = RefreshAsync(userId, refresh);

        return await refreshTask.WaitAsync(cancellationToken);
    }

    private bool TryGetCachedPermissions(
        string userId,
        DateTimeOffset now,
        out IReadOnlySet<string>? permissions)
    {
        permissions = null;
        var key = CacheKeyPrefix + userId;
        if (!_cache.TryGetValue(key, out CachedPermissions? cached) || cached is null)
            return false;

        var age = now - cached.StoredAt;
        var lifetime = cached.Permissions is null ? FailureBackoff : SnapshotLifetime;
        if (age >= TimeSpan.Zero && age < lifetime)
        {
            permissions = cached.Permissions;
            return true;
        }

        _cache.Remove(key);
        return false;
    }

    private async Task RefreshAsync(
        string userId,
        TaskCompletionSource<IReadOnlySet<string>?> completion)
    {
        IReadOnlySet<string>? permissions = null;
        var hasRefreshPermit = false;
        using var timeout = new CancellationTokenSource(RefreshTimeout);
        try
        {
            // This application-side cap does not assume or represent an Auth0 tenant quota.
            await _refreshLimit.WaitAsync(timeout.Token);
            hasRefreshPermit = true;

            using var scope = scopeFactory.CreateScope();
            var managementService = scope.ServiceProvider.GetService<IAuth0ManagementService>();
            if (managementService is null)
            {
                PermissionSnapshotLogger.RefreshFailed(
                    logger,
                    "ManagementServiceUnavailable",
                    null,
                    null);
            }
            else
            {
                var grants = await managementService
                    .GetUserPermissionsAsync(userId, timeout.Token)
                    .WaitAsync(timeout.Token);
                permissions = grants.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or TimeoutException)
        {
            PermissionSnapshotLogger.RefreshFailed(
                logger,
                "Timeout",
                exception.GetType().Name,
                null);
        }
        catch (HttpRequestException exception)
        {
            PermissionSnapshotLogger.RefreshFailed(
                logger,
                "HttpFailure",
                exception.GetType().Name,
                exception.StatusCode is { } statusCode ? (int)statusCode : null);
        }
        catch (Exception exception)
        {
            PermissionSnapshotLogger.RefreshFailed(
                logger,
                "RefreshFailure",
                exception.GetType().Name,
                null);
        }
        finally
        {
            if (hasRefreshPermit)
                _refreshLimit.Release();
        }

        var lifetime = permissions is null ? FailureBackoff : SnapshotLifetime;
        _cache.Set(
            CacheKeyPrefix + userId,
            new CachedPermissions(permissions, timeProvider.GetUtcNow()),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = lifetime,
                Size = 1
            });

        lock (_inFlightLock)
        {
            _inFlight.Remove(userId);
            completion.TrySetResult(permissions);
        }
    }

    private sealed record CachedPermissions(
        IReadOnlySet<string>? Permissions,
        DateTimeOffset StoredAt);
}

internal static partial class PermissionSnapshotLogger
{
    [LoggerMessage(
        LogLevel.Warning,
        "Permission refresh failed; the request was denied. Category: {FailureCategory}; Exception type: {ExceptionType}; HTTP status: {HttpStatusCode}.")]
    internal static partial void RefreshFailed(
        ILogger logger,
        string failureCategory,
        string? exceptionType,
        int? httpStatusCode);
}
