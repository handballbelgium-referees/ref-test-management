namespace Handball.Belgium.RefTestManagement.Application.Models;

/// <summary>
/// Configuration for the RefTest Expiration background service
/// </summary>
public class RefTestExpirationConfiguration
{
    /// <summary>
    /// How often to check for expired RefTests (in minutes)
    /// </summary>
    public int ExpirationCheckIntervalMinutes { get; init; } = 5;
    
    /// <summary>
    /// Startup delay before the first expiration check (in seconds)
    /// </summary>
    public int StartupDelaySeconds { get; init; } = 30;
    
    /// <summary>
    /// How long a RefTest is valid if not started (TimeSpan format: d.hh:mm:ss)
    /// </summary>
    public TimeSpan ExpirationIfNotStarted { get; init; } = TimeSpan.FromDays(7);
}
