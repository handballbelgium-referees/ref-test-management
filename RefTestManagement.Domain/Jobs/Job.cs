namespace Handball.Belgium.RefTestManagement.Domain.Jobs;

/// <summary>
/// Represents a background job to be processed
/// </summary>
public class Job
{
    private Job(JobType jobType, string payload, DateTime executeAfter)
    {
        Id = Guid.NewGuid();
        JobType = jobType;
        Payload = payload;
        Status = JobStatus.Pending;
        Attempts = 0;
        CreatedAt = DateTime.UtcNow;
        ExecuteAfter = executeAfter;
    }

    public Guid Id { get; private set; }
    public JobType JobType { get; private set; }
    public string Payload { get; private set; }
    public JobStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExecuteAfter { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Creates a new job
    /// </summary>
    public static Job Create(JobType jobType, string payload, DateTime? executeAfter = null)
    {
        return new Job(jobType, payload, executeAfter ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Marks the job as processing and locks it
    /// </summary>
    public void MarkAsProcessing(TimeSpan lockDuration)
    {
        Status = JobStatus.Processing;
        LockedUntil = DateTime.UtcNow.Add(lockDuration);
    }

    /// <summary>
    /// Marks the job as completed
    /// </summary>
    public void MarkAsCompleted()
    {
        Status = JobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        LockedUntil = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks the job as failed and increments attempts
    /// </summary>
    public void MarkAsFailed(string errorMessage, int maxAttempts)
    {
        Attempts++;
        ErrorMessage = errorMessage;

        Status = Attempts >= maxAttempts ? JobStatus.Failed : JobStatus.Pending;

        LockedUntil = null;
    }

    /// <summary>
    /// Cancels the job by marking it as failed
    /// </summary>
    public void Cancel(string reason)
    {
        Status = JobStatus.Failed;
        ErrorMessage = reason;
        LockedUntil = null;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the job is ready to be processed
    /// </summary>
    public bool IsReadyToProcess()
    {
        return Status == JobStatus.Pending
               && ExecuteAfter <= DateTime.UtcNow
               && (LockedUntil == null || LockedUntil <= DateTime.UtcNow);
    }
}

