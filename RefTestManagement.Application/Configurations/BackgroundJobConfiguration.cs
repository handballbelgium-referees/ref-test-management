namespace Handball.Belgium.RefTestManagement.Application.Configurations;

/// <summary>
/// Configuration for the BackgroundJobService
/// </summary>
public class BackgroundJobConfiguration
{
    /// <summary>
    /// How often to poll for new jobs (in seconds)
    /// </summary>
    public int PollingIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// How long to lock a job when processing (in minutes)
    /// </summary>
    public int LockDurationMinutes { get; init; } = 5;

    /// <summary>
    /// Maximum number of retry attempts for a failed job
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// How many jobs to process in a single batch
    /// </summary>
    public int BatchSize { get; init; } = 10;

    /// <summary>
    /// Startup delay before starting job processing (in seconds)
    /// </summary>
    public int StartupDelaySeconds { get; init; } = 10;

    /// <summary>
    /// Whether to enable automatic cleanup of old jobs
    /// </summary>
    public bool EnableCleanup { get; init; } = true;

    /// <summary>
    /// How often to run the cleanup (in hours)
    /// </summary>
    public int CleanupIntervalHours { get; init; } = 24;

    /// <summary>
    /// How long to retain completed jobs before deletion (in days)
    /// </summary>
    public int RetainCompletedJobsDays { get; init; } = 7;

    /// <summary>
    /// How long to retain failed jobs before deletion (in days)
    /// </summary>
    public int RetainFailedJobsDays { get; init; } = 30;
}
