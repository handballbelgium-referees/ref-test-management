using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Checks a single permission directly against the identity provider, bypassing the claim cache.
/// Any timeout, provider error, or unidentifiable user results in <c>false</c> (fail closed).
/// </summary>
public interface ILivePermissionVerifier
{
    /// <summary>
    /// Returns <see langword="true"/> only when the provider confirms that the user currently holds
    /// <paramref name="requiredPermission"/> (directly, via namespace wildcard, or via superadmin).
    /// </summary>
    Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string requiredPermission,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ILivePermissionVerifier"/> that resolves <see cref="IIdentityPermissionSync"/>
/// per call and applies <see cref="LivePermissionTimeoutSecondsKey"/> as an upper bound.
/// </summary>
public sealed class LivePermissionVerifier(
    IServiceScopeFactory scopeFactory,
    ILogger<LivePermissionVerifier> logger,
    IConfiguration configuration) : ILivePermissionVerifier
{
    public const string LivePermissionTimeoutSecondsKey = "Identity:LivePermissionTimeoutSeconds";
    public const int DefaultLivePermissionTimeoutSeconds = 2;

    private readonly TimeSpan _timeout = ReadTimeout(configuration);

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string requiredPermission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermission);

        var subject = user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject?.Value))
        {
            LivePermissionLogger.CheckFailed(logger, "MissingSubject", null);
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var sync = scope.ServiceProvider.GetService<IIdentityPermissionSync>();
            if (sync is null)
            {
                LivePermissionLogger.CheckFailed(logger, "ProviderUnavailable", null);
                return false;
            }

            var granted = await sync.GetUserPermissionsAsync(subject.Value, timeout.Token);
            return PermissionMatcher.IsSatisfied(granted, requiredPermission);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LivePermissionLogger.CheckFailed(logger, "Timeout", null);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LivePermissionLogger.CheckFailed(logger, "ProviderFailure", exception.GetType().Name);
            return false;
        }
    }

    private static TimeSpan ReadTimeout(IConfiguration configuration)
    {
        var seconds = configuration.GetValue(
            LivePermissionTimeoutSecondsKey,
            DefaultLivePermissionTimeoutSeconds);
        if (seconds <= 0)
            throw new InvalidOperationException(
                $"{LivePermissionTimeoutSecondsKey} must be a positive number of seconds.");

        return TimeSpan.FromSeconds(seconds);
    }
}

internal static partial class LivePermissionLogger
{
    [LoggerMessage(
        LogLevel.Warning,
        "Live permission check failed closed. Category: {FailureCategory}; Exception type: {ExceptionType}.")]
    internal static partial void CheckFailed(ILogger logger, string failureCategory, string? exceptionType);
}
