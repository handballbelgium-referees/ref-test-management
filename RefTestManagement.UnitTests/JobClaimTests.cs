using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers <c>BackgroundJobService.ClaimJobAsync</c> against a real database.
/// </summary>
/// <remarks>
/// These run against SQLite rather than a fake because the whole point of the claim is that the
/// database — not the application — decides who wins. A test that does not execute SQL cannot say
/// anything about that.
/// </remarks>
public class JobClaimTests
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 3;

    private static async Task<Guid> SeedPendingJobAsync(SqliteTestDatabase database)
    {
        await using var context = database.CreateContext();
        var job = Job.Create(JobType.InvitationEmail, "{}");
        context.Jobs.Add(job);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return job.Id;
    }

    [Fact]
    public async Task ClaimingAPendingJobSucceedsAndLocksIt()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedPendingJobAsync(database);

        await using var context = database.CreateContext();
        var claimed = await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        Assert.NotNull(claimed);
        Assert.Equal(JobStatus.Processing, claimed.Status);
        Assert.NotNull(claimed.LockedUntil);
        Assert.True(claimed.LockedUntil > DateTime.UtcNow);
    }

    /// <summary>
    /// The reason this work package exists: before the claim was a single statement, two instances
    /// could both read a Pending job and both send its email.
    /// </summary>
    [Fact]
    public async Task OnlyOneOfTwoConcurrentWorkersCanClaimTheSameJob()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedPendingJobAsync(database);

        await using var firstWorker = database.CreateContext();
        await using var secondWorker = database.CreateContext();

        var firstClaim = await BackgroundJobService.ClaimJobAsync(
            firstWorker, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);
        var secondClaim = await BackgroundJobService.ClaimJobAsync(
            secondWorker, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        Assert.NotNull(firstClaim);
        Assert.Null(secondClaim);
    }

    [Fact]
    public async Task AJobWhoseLockHasExpiredIsReclaimable()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedPendingJobAsync(database);

        await using var context = database.CreateContext();
        await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        // Stand in for a worker that died holding the lock.
        await context.Jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(j => j.LockedUntil, DateTime.UtcNow.AddMinutes(-1)),
                TestContext.Current.CancellationToken);

        var reclaimed = await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        Assert.NotNull(reclaimed);
    }

    /// <summary>
    /// The conditional attempt increment is the one part of the claim that is easy to mistranslate,
    /// because it has to read the status as it was before the same statement overwrote it.
    /// </summary>
    [Fact]
    public async Task ReclaimingAStrandedJobSpendsAnAttemptButAFirstClaimDoesNot()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedPendingJobAsync(database);

        await using var context = database.CreateContext();

        var firstClaim = await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);
        Assert.Equal(0, firstClaim!.Attempts);

        await context.Jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(j => j.LockedUntil, DateTime.UtcNow.AddMinutes(-1)),
                TestContext.Current.CancellationToken);

        var reclaim = await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        Assert.Equal(1, reclaim!.Attempts);
    }

    /// <summary>
    /// Without this bound a job that strands on every pass would be retried forever.
    /// </summary>
    [Fact]
    public async Task AJobThatHasSpentItsAttemptsIsNotClaimable()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedPendingJobAsync(database);

        await using var context = database.CreateContext();
        await context.Jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(j => j.Attempts, MaxAttempts),
                TestContext.Current.CancellationToken);

        var claimed = await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        Assert.Null(claimed);
    }

    [Fact]
    public async Task AJobScheduledForTheFutureIsNotClaimable()
    {
        using var database = SqliteTestDatabase.Create();

        Guid jobId;
        await using (var seedContext = database.CreateContext())
        {
            var job = Job.Create(JobType.ResultEmail, "{}", DateTime.UtcNow.AddHours(1));
            seedContext.Jobs.Add(job);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            jobId = job.Id;
        }

        await using var context = database.CreateContext();
        var claimed = await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        Assert.Null(claimed);
    }

    [Fact]
    public async Task ACancelledJobIsNotClaimable()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedPendingJobAsync(database);

        await using (var cancelContext = database.CreateContext())
        {
            var job = await cancelContext.Jobs.FirstAsync(
                j => j.Id == jobId, TestContext.Current.CancellationToken);
            job.Cancel("no longer needed");
            await cancelContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        var claimed = await BackgroundJobService.ClaimJobAsync(
            context, jobId, MaxAttempts, LockDuration, TestContext.Current.CancellationToken);

        Assert.Null(claimed);
    }
}
