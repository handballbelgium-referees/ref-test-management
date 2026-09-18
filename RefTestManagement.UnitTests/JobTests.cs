using Handball.Belgium.RefTestManagement.Domain.Jobs;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers the job state machine. The regression these tests exist for: cancelling a job clears
/// its payload, so any transition that returns a cancelled job to <see cref="JobStatus.Pending"/>
/// produces a runnable job with nothing to deserialize — which broke <c>resetRefTest</c> and
/// <c>reviveRefTest</c> for every other RefTest in the table, not just the cancelled one.
/// </summary>
public class JobTests
{
    private const int MaxAttempts = 3;

    private static Job NewJob() =>
        Job.Create(JobType.InvitationEmail, """{"refTestId":"00000000-0000-0000-0000-000000000001"}""");

    [Fact]
    public void Create_StartsPendingWithItsPayload()
    {
        var job = NewJob();

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(0, job.Attempts);
        Assert.NotEmpty(job.Payload);
    }

    [Fact]
    public void Cancel_MarksTheJobCancelledAndClearsThePayload()
    {
        var job = NewJob();

        job.Cancel("RefTest was deleted");

        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.Equal(string.Empty, job.Payload);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void MarkAsFailed_AfterCancel_DoesNotReviveTheJob()
    {
        // The regression. A worker holding the job when it was cancelled fails afterwards; with
        // Attempts below the maximum the old code wrote Pending back over the cancellation.
        var job = NewJob();
        job.MarkAsProcessing(TimeSpan.FromMinutes(5));
        job.Cancel("RefTest was deleted");

        job.MarkAsFailed("send failed", MaxAttempts);

        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.Equal(string.Empty, job.Payload);
    }

    [Fact]
    public void ACancelledJobIsNeverReadyToProcess()
    {
        var job = NewJob();
        job.Cancel("RefTest was deleted");
        job.MarkAsFailed("send failed", MaxAttempts);

        Assert.False(job.IsReadyToProcess());
    }

    [Fact]
    public void MarkAsFailed_RetriesUntilTheAttemptLimit_ThenStopsPermanently()
    {
        var job = NewJob();

        job.MarkAsFailed("first", MaxAttempts);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Null(job.CompletedAt);

        job.MarkAsFailed("second", MaxAttempts);
        Assert.Equal(JobStatus.Pending, job.Status);

        job.MarkAsFailed("third", MaxAttempts);
        Assert.Equal(JobStatus.Failed, job.Status);

        // Cleanup keys its retention window off CompletedAt, so a terminal job that leaves it
        // null is retained forever.
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void MarkAsPermanentlyFailed_DoesNotWaitForTheAttemptLimit()
    {
        var job = NewJob();

        job.MarkAsPermanentlyFailed("payload could not be deserialized");

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void MarkAsPermanentlyFailed_AfterCancel_DoesNotReviveTheJob()
    {
        var job = NewJob();
        job.Cancel("RefTest was deleted");

        job.MarkAsPermanentlyFailed("payload could not be deserialized");

        Assert.Equal(JobStatus.Cancelled, job.Status);
    }

    [Fact]
    public void AProcessingJobWithALiveLockIsNotReclaimed()
    {
        var job = NewJob();
        job.MarkAsProcessing(TimeSpan.FromMinutes(5));

        Assert.False(job.IsReadyToProcess());
    }

    [Fact]
    public void AProcessingJobWithAnExpiredLockIsReclaimed()
    {
        // Nothing else recovers a job whose worker never reported back: cleanup only deletes
        // Completed/Failed/Cancelled rows.
        var job = NewJob();
        job.MarkAsProcessing(TimeSpan.FromMinutes(-5));

        Assert.True(job.IsReadyToProcess());
    }

    [Fact]
    public void ReclaimingAStrandedJobSpendsAnAttempt()
    {
        // Otherwise a job that strands on every pass is reclaimed forever.
        var job = NewJob();
        job.MarkAsProcessing(TimeSpan.FromMinutes(-5));
        Assert.Equal(0, job.Attempts);

        job.MarkAsProcessing(TimeSpan.FromMinutes(-5));

        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public void MarkAsCompleted_ClearsTheLockAndTheError()
    {
        var job = NewJob();
        job.MarkAsProcessing(TimeSpan.FromMinutes(5));
        job.MarkAsFailed("transient", MaxAttempts);

        job.MarkAsCompleted();

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Null(job.LockedUntil);
        Assert.Null(job.ErrorMessage);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void CancelledIsTheLastMemberOfJobStatus()
    {
        // JobStatus declares no explicit values, so EF persists it by ordinal. Inserting a
        // member anywhere but the end silently reinterprets every existing row.
        Assert.Equal(0, (int)JobStatus.Pending);
        Assert.Equal(1, (int)JobStatus.Processing);
        Assert.Equal(2, (int)JobStatus.Completed);
        Assert.Equal(3, (int)JobStatus.Failed);
        Assert.Equal(4, (int)JobStatus.Cancelled);
    }

    [Fact]
    public void AJobIsNotReadyBeforeItsScheduledTime()
    {
        var job = Job.Create(JobType.RefTestExpiration, "{}", DateTime.UtcNow.AddHours(1));

        Assert.False(job.IsReadyToProcess());
    }
}
