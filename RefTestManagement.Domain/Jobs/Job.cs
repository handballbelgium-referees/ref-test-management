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
    /// Marks the job as processing and locks it. Re-locking a job that is already
    /// <see cref="JobStatus.Processing"/> means its previous lock expired without the worker
    /// reporting back, so that counts as a spent attempt — otherwise a job that strands on every
    /// pass would be reclaimed forever.
    /// </summary>
    public void MarkAsProcessing(TimeSpan lockDuration)
    {
        if (Status == JobStatus.Processing)
            Attempts++;

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
        // A cancelled job is terminal. Without this guard a worker that was already holding the
        // job when it was cancelled would return it to Pending on its next failure, and because
        // Cancel() clears the payload that leaves a runnable job with nothing to deserialize.
        if (Status == JobStatus.Cancelled)
            return;

        Attempts++;
        ErrorMessage = errorMessage;

        Status = Attempts >= maxAttempts ? JobStatus.Failed : JobStatus.Pending;

        // Stamp the terminal transition: cleanup keys its retention window off CompletedAt, so a
        // failed job that never sets it is retained forever.
        if (Status == JobStatus.Failed)
            CompletedAt = DateTime.UtcNow;

        LockedUntil = null;
    }

    /// <summary>
    /// Fails the job immediately, without leaving retries on the table. For errors that cannot
    /// possibly succeed on a retry — a payload that does not parse is the same payload next time.
    /// </summary>
    public void MarkAsPermanentlyFailed(string errorMessage)
    {
        if (Status == JobStatus.Cancelled)
            return;

        Attempts++;
        ErrorMessage = errorMessage;
        Status = JobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        LockedUntil = null;
    }

    /// <summary>
    /// Cancels the job. Clears <see cref="Payload"/>: a cancelled job will never run, so its
    /// payload has no further use, and for participant-facing jobs it holds personal data (name,
    /// email, invitation token, scores, answers) that must not survive an erasure request.
    /// Callers that need the payload must read it before cancelling.
    /// </summary>
    public void Cancel(string reason)
    {
        Status = JobStatus.Cancelled;
        ErrorMessage = reason;
        LockedUntil = null;
        CompletedAt = DateTime.UtcNow;
        Payload = string.Empty;
    }

    /// <summary>
    /// Checks if the job is ready to be processed. A <see cref="JobStatus.Processing"/> job whose
    /// lock has expired is included: its worker never reported back, and nothing else recovers it.
    /// </summary>
    public bool IsReadyToProcess()
    {
        if (Status != JobStatus.Pending && Status != JobStatus.Processing)
            return false;

        return ExecuteAfter <= DateTime.UtcNow
               && (LockedUntil == null || LockedUntil <= DateTime.UtcNow);
    }
}

