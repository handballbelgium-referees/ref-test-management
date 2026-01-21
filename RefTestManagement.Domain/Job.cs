namespace Handball.Belgium.RefTestManagement.Domain;

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
        
        if (Attempts >= maxAttempts)
        {
            Status = JobStatus.Failed;
            LockedUntil = null;
        }
        else
        {
            Status = JobStatus.Pending;
            LockedUntil = null;
        }
    }

    /// <summary>
    /// Releases the lock on the job
    /// </summary>
    public void ReleaseLock()
    {
        LockedUntil = null;
        Status = JobStatus.Pending;
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
