using System.Collections.Frozen;
using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>
/// Resolves a user's current effective permissions from the configured identity provider.
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
/// Caches effective identity-provider permissions briefly and shares concurrent refreshes per user.
/// </summary>
public sealed class PermissionSnapshotService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PermissionSnapshotService> logger,
    IConfiguration configuration)
    : IPermissionSnapshotService
{
    internal const int MaximumConcurrentRefreshes = 4;
    // Leaves room for 60 queued subjects beyond the active calls; each refresh expires within 30 seconds.
    internal const int MaximumPendingRefreshes = 64;
    // One size unit per subject keeps this cache bounded independently of other application caches.
    internal const int MaximumCachedSnapshots = 1_024;

    internal const string PermissionCacheSecondsKey = "Identity:PermissionCacheSeconds";
    internal const int DefaultPermissionCacheSeconds = 240;

    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(30);
    // Includes semaphore queue time and the complete multi-request provider lookup.
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(30);
    private const string CacheKeyPrefix = "PermissionSnapshot:";

    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = MaximumCachedSnapshots
    });
    private readonly object _inFlightLock = new();
    private readonly Dictionary<string, TaskCompletionSource<IReadOnlySet<string>?>> _inFlight =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refreshLimit = new(MaximumConcurrentRefreshes);
    private readonly TimeSpan _snapshotLifetime = ReadSnapshotLifetime(configuration);

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>?> GetCurrentPermissionsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.Identities.Any(identity => identity.IsAuthenticated))
            return null;

        var subjectClaim = principal.FindFirst("sub");
        if (subjectClaim is null || string.IsNullOrWhiteSpace(subjectClaim.Value))
            subjectClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        var userId = subjectClaim?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            PermissionSnapshotLogger.RefreshFailed(logger, "MissingSubject", null, null);
            return null;
        }

        // Scoped by issuer so an identical subject from another provider never shares a snapshot.
        var subjectKey = (subjectClaim?.Issuer ?? string.Empty) + "|" + userId;

        Task<IReadOnlySet<string>?>? refreshTask = null;
        TaskCompletionSource<IReadOnlySet<string>?>? refresh = null;
        lock (_inFlightLock)
        {
            if (TryGetCachedPermissions(subjectKey, timeProvider.GetUtcNow(), out var cachedPermissions))
                return cachedPermissions;

            if (_inFlight.TryGetValue(subjectKey, out var inFlight))
            {
                refreshTask = inFlight.Task;
            }
            else if (_inFlight.Count < MaximumPendingRefreshes)
            {
                refresh = new TaskCompletionSource<IReadOnlySet<string>?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _inFlight.Add(subjectKey, refresh);
                refreshTask = refresh.Task;
            }
        }

        if (refreshTask is null)
        {
            PermissionSnapshotLogger.RefreshFailed(logger, "Capacity", null, null);
            return null;
        }

        if (refresh is not null)
            _ = RefreshAsync(userId, subjectKey, refresh);

        return await refreshTask.WaitAsync(cancellationToken);
    }

    private bool TryGetCachedPermissions(
        string subjectKey,
        DateTimeOffset now,
        out IReadOnlySet<string>? permissions)
    {
        permissions = null;
        var key = CacheKeyPrefix + subjectKey;
        if (!_cache.TryGetValue(key, out CachedPermissions? cached) || cached is null)
            return false;

        var age = now - cached.StoredAt;
        var lifetime = cached.Permissions is null ? FailureBackoff : _snapshotLifetime;
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
        string subjectKey,
        TaskCompletionSource<IReadOnlySet<string>?> completion)
    {
        IReadOnlySet<string>? permissions = null;
        var hasRefreshPermit = false;
        using var timeout = new CancellationTokenSource(RefreshTimeout);
        try
        {
            // This application-side cap does not assume or represent a provider quota.
            await _refreshLimit.WaitAsync(timeout.Token);
            hasRefreshPermit = true;

            using var scope = scopeFactory.CreateScope();
            var managementService = scope.ServiceProvider.GetService<IIdentityPermissionSync>();
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

        var lifetime = permissions is null ? FailureBackoff : _snapshotLifetime;
        _cache.Set(
            CacheKeyPrefix + subjectKey,
            new CachedPermissions(permissions, timeProvider.GetUtcNow()),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = lifetime,
                Size = 1
            });

        lock (_inFlightLock)
        {
            _inFlight.Remove(subjectKey);
            completion.TrySetResult(permissions);
        }
    }

    private static TimeSpan ReadSnapshotLifetime(IConfiguration configuration)
    {
        var seconds = configuration.GetValue(PermissionCacheSecondsKey, DefaultPermissionCacheSeconds);
        if (seconds <= 0)
            throw new InvalidOperationException(
                $"{PermissionCacheSecondsKey} must be a positive number of seconds.");

        return TimeSpan.FromSeconds(seconds);
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
