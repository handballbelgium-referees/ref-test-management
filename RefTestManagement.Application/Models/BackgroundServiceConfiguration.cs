namespace Handball.Belgium.RefTestManagement.Application.Models;

/// <summary>
/// Configuration for background services
/// </summary>
public class BackgroundServiceConfiguration
{
    public int ExpirationCheckIntervalMinutes { get; init; } = 5;
    public int StartupDelaySeconds { get; init; } = 30;
    public TimeSpan ExpirationIfNotStarted { get; init; } = TimeSpan.FromDays(7);
}

