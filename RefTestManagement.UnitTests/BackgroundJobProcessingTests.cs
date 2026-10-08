using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers <c>BackgroundJobService.ProcessJobAsync</c>: what the queue does with a job once a
/// handler has had its turn.
/// </summary>
/// <remarks>
/// This is the failure policy, and until the handlers were split out behind
/// <see cref="IJobHandler"/> it could only be reached by sending a real email. Now a test can
/// register a handler that throws exactly what it wants to ask about, which is the point of the
/// refactor: the branch that decides retry-versus-give-up is the branch most likely to strand a
/// job or spam a participant, and it was the one branch with no coverage.
/// </remarks>
public class BackgroundJobProcessingTests
{
    private const int MaxAttempts = 3;

    /// <summary>A handler that does whatever the test tells it to.</summary>
    private sealed class StubHandler(Func<Job, Task> behaviour) : IJobHandler
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(Job job, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return behaviour(job);
        }
    }

    private static IServiceProvider ProviderFor(JobType jobType, IJobHandler handler)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton(jobType, handler);
        return services.BuildServiceProvider();
    }

    private static IServiceProvider ProviderWithNoHandlers() =>
        new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// Seeds a job that has already been claimed, which is the only state ProcessJobAsync is ever
    /// handed one in.
    /// </summary>
    private static async Task<Guid> SeedClaimedJobAsync(
        SqliteTestDatabase database,
        JobType jobType = JobType.InvitationEmail)
    {
        await using var context = database.CreateContext();
        var job = Job.Create(jobType, "{}");
        job.MarkAsProcessing(TimeSpan.FromMinutes(5));
        context.Jobs.Add(job);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return job.Id;
    }

    private static async Task<Job> RunAsync(
        SqliteTestDatabase database,
        Guid jobId,
        IServiceProvider serviceProvider)
    {
        await using var context = database.CreateContext();
        var job = await context.Jobs.FindAsync([jobId], TestContext.Current.CancellationToken);

        await BackgroundJobService.ProcessJobAsync(
            job!,
            serviceProvider,
            context,
            NullLogger.Instance,
            MaxAttempts,
            TestContext.Current.CancellationToken);

        await using var verificationContext = database.CreateContext();
        return (await verificationContext.Jobs.FindAsync([jobId], TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task AHandlerThatReturnsMarksTheJobCompleted()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database);
        var handler = new StubHandler(_ => Task.CompletedTask);

        var job = await RunAsync(database, jobId, ProviderFor(JobType.InvitationEmail, handler));

        Assert.True(handler.WasCalled);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.LockedUntil);
    }

    /// <summary>
    /// The retry path. A transient failure must return the job to Pending with its lock released,
    /// or the next poll will skip it until the lease expires.
    /// </summary>
    [Fact]
    public async Task ATransientFailureReturnsTheJobToThePendingQueue()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database);
        var handler = new StubHandler(_ => throw new InvalidOperationException("SMTP timed out"));

        var job = await RunAsync(database, jobId, ProviderFor(JobType.InvitationEmail, handler));

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Null(job.LockedUntil);
        Assert.Equal("SMTP timed out", job.ErrorMessage);
    }

    /// <summary>
    /// A handler exception must never escape. If it did, the polling loop would abandon the rest of
    /// the batch and the job would be stranded at Processing until its lease ran out.
    /// </summary>
    [Fact]
    public async Task AHandlerExceptionDoesNotEscapeToTheCaller()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database);
        var handler = new StubHandler(_ => throw new InvalidOperationException("boom"));

        var job = await RunAsync(database, jobId, ProviderFor(JobType.InvitationEmail, handler));

        Assert.NotEqual(JobStatus.Processing, job.Status);
    }

    [Fact]
    public async Task RepeatedFailuresStopAtTheAttemptLimit()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database);
        var provider = ProviderFor(
            JobType.InvitationEmail,
            new StubHandler(_ => throw new InvalidOperationException("still broken")));

        Job job = null!;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // Re-claim between passes the way the polling loop would.
            await using (var context = database.CreateContext())
            {
                var pending = await context.Jobs.FindAsync([jobId], TestContext.Current.CancellationToken);
                if (pending!.Status == JobStatus.Pending)
                {
                    pending.MarkAsProcessing(TimeSpan.FromMinutes(5));
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }

            job = await RunAsync(database, jobId, provider);
        }

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(MaxAttempts, job.Attempts);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public async Task ExpiredFinalAttemptLeaseFailsAndClearsExportRequestAsUnknown()
    {
        using var database = SqliteTestDatabase.Create();
        var now = DateTime.UtcNow;
        var key = new string('K', 43);
        var request = PersonalDataExportRequest.Create(
            "ada@example.org",
            key,
            $"protected:{key}",
            now.AddMinutes(-1),
            now.AddHours(1));
        Assert.True(request.TryConfirm(key, now));
        var payload = JsonSerializer.Serialize(
            new PersonalDataExportDeliveryEmailPayload(request.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var job = Job.Create(JobType.PersonalDataExportDeliveryEmail, payload);
        job.MarkAsProcessing(TimeSpan.FromMinutes(-1));

        await using (var seed = database.CreateContext())
        {
            seed.PersonalDataExportRequests.Add(request);
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        PersonalDataExportRequest finalizedRequest;
        await using (var context = database.CreateContext())
        {
            var reclaimed = await BackgroundJobService.ClaimJobAsync(
                context,
                job.Id,
                maxAttempts: 1,
                TimeSpan.FromMinutes(5),
                TestContext.Current.CancellationToken);
            Assert.Null(reclaimed);

            var failed = await BackgroundJobService.FailExpiredFinalAttemptJobsAsync(
                context,
                NullLogger.Instance,
                maxAttempts: 1,
                batchSize: 10,
                TestContext.Current.CancellationToken);
            Assert.Equal(1, failed);
            finalizedRequest = await context.PersonalDataExportRequests.SingleAsync(
                candidate => candidate.Id == request.Id,
                TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var failedJob = await verification.Jobs.SingleAsync(
            candidate => candidate.Id == job.Id,
            TestContext.Current.CancellationToken);
        var clearedRequest = await verification.PersonalDataExportRequests.SingleAsync(
            candidate => candidate.Id == request.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Failed, failedJob.Status);
        Assert.Equal(1, failedJob.Attempts);
        Assert.NotNull(failedJob.CompletedAt);
        Assert.DoesNotContain("ada@example.org", failedJob.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, clearedRequest.Email);
        Assert.Null(clearedRequest.KeyHash);
        Assert.Null(clearedRequest.ProtectedDeliveryKey);
        var terminalFailure = Assert.IsType<PersonalDataExportDeliveryFailedEvent>(
            Assert.Single(finalizedRequest.DomainEvents.OfType<PersonalDataExportDeliveryFailedEvent>()));
        Assert.Equal(PersonalDataExportDeliveryFailureCode.DeliveryOutcomeUnknown, terminalFailure.FailureCode);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Processing)]
    public async Task ExpiredFinalAttemptLeasePreservesRequestUntilActiveDuplicateFinishes(
        JobStatus duplicateStatus)
    {
        using var database = SqliteTestDatabase.Create();
        var now = DateTime.UtcNow;
        var key = new string('K', 43);
        var request = PersonalDataExportRequest.Create(
            "ada@example.org",
            key,
            $"protected:{key}",
            now.AddMinutes(-1),
            now.AddHours(1));
        Assert.True(request.TryConfirm(key, now));
        var payload = JsonSerializer.Serialize(
            new PersonalDataExportDeliveryEmailPayload(request.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var expiredJob = Job.Create(JobType.PersonalDataExportDeliveryEmail, payload);
        expiredJob.MarkAsProcessing(TimeSpan.FromMinutes(-1));
        var duplicateJob = Job.Create(JobType.PersonalDataExportDeliveryEmail, payload);
        if (duplicateStatus == JobStatus.Processing)
            duplicateJob.MarkAsProcessing(TimeSpan.FromMinutes(5));

        await using (var seed = database.CreateContext())
        {
            seed.PersonalDataExportRequests.Add(request);
            seed.Jobs.AddRange(expiredJob, duplicateJob);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var failed = await BackgroundJobService.FailExpiredFinalAttemptJobsAsync(
                context,
                NullLogger.Instance,
                maxAttempts: 1,
                batchSize: 10,
                TestContext.Current.CancellationToken);
            Assert.Equal(1, failed);
        }

        await using (var verification = database.CreateContext())
        {
            var preserved = await verification.PersonalDataExportRequests.SingleAsync(
                candidate => candidate.Id == request.Id,
                TestContext.Current.CancellationToken);
            var activeDuplicate = await verification.Jobs.SingleAsync(
                candidate => candidate.Id == duplicateJob.Id,
                TestContext.Current.CancellationToken);
            Assert.Equal(duplicateStatus, activeDuplicate.Status);
            Assert.Equal("ada@example.org", preserved.Email);
            Assert.NotNull(preserved.VerifiedAt);
            Assert.Empty(preserved.DomainEvents);
        }

        await using (var context = database.CreateContext())
        {
            var activeDuplicate = await context.Jobs.SingleAsync(
                candidate => candidate.Id == duplicateJob.Id,
                TestContext.Current.CancellationToken);
            if (activeDuplicate.Status == JobStatus.Pending)
                activeDuplicate.MarkAsProcessing(TimeSpan.FromMinutes(5));
            activeDuplicate.MarkAsFailed("Personal-data export delivery failed.", maxAttempts: 1);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var trackedRequest = await context.PersonalDataExportRequests.SingleAsync(
                candidate => candidate.Id == request.Id,
                TestContext.Current.CancellationToken);
            var cleared = await PersonalDataExportRequestCleanupService.ClearExpiredChallengesAsync(
                context,
                DateTime.UtcNow,
                TestContext.Current.CancellationToken);
            Assert.Equal(1, cleared);
            Assert.Equal(string.Empty, trackedRequest.Email);
            Assert.Null(trackedRequest.KeyHash);
            Assert.Null(trackedRequest.ProtectedDeliveryKey);
            var terminalFailure = Assert.IsType<PersonalDataExportDeliveryFailedEvent>(
                Assert.Single(trackedRequest.DomainEvents.OfType<PersonalDataExportDeliveryFailedEvent>()));
            Assert.Equal(PersonalDataExportDeliveryFailureCode.DeliveryOutcomeUnknown, terminalFailure.FailureCode);
            Assert.True(terminalFailure.IsTerminal);

            Assert.Equal(
                0,
                await PersonalDataExportRequestCleanupService.ClearExpiredChallengesAsync(
                    context,
                    DateTime.UtcNow,
                    TestContext.Current.CancellationToken));
            Assert.Single(trackedRequest.DomainEvents.OfType<PersonalDataExportDeliveryFailedEvent>());
        }
    }

    [Fact]
    public async Task ExpiredNonFinalLeaseRemainsEligibleForRetry()
    {
        using var database = SqliteTestDatabase.Create();
        var job = Job.Create(JobType.InvitationEmail, "{}");
        job.MarkAsProcessing(TimeSpan.FromMinutes(-1));

        await using (var seed = database.CreateContext())
        {
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        var failed = await BackgroundJobService.FailExpiredFinalAttemptJobsAsync(
            context,
            NullLogger.Instance,
            maxAttempts: 2,
            batchSize: 10,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, failed);

        var reclaimed = await BackgroundJobService.ClaimJobAsync(
            context,
            job.Id,
            maxAttempts: 2,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.NotNull(reclaimed);
        Assert.Equal(JobStatus.Processing, reclaimed.Status);
        Assert.Equal(1, reclaimed.Attempts);
        Assert.True(reclaimed.LockedUntil > DateTime.UtcNow);
    }

    /// <summary>
    /// A payload that will not parse will not parse on the next pass either. Retrying it burns two
    /// more slots and two more log entries to reach the same answer.
    /// </summary>
    [Fact]
    public async Task AnUnreadablePayloadFailsPermanentlyOnTheFirstAttempt()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database);
        var handler = new StubHandler(job => throw new JobPayloadException($"Job {job.Id} has no payload"));

        var job = await RunAsync(database, jobId, ProviderFor(JobType.InvitationEmail, handler));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.True(job.Attempts < MaxAttempts);
    }

    /// <summary>
    /// ErrorMessage is not reached by the erasure path, so an address that reaches it survives a
    /// deletion request. Third-party mail APIs put the recipient in their failure messages.
    /// </summary>
    [Fact]
    public async Task AnAddressInAFailureMessageIsMaskedBeforeItIsStored()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database);
        var handler = new StubHandler(
            _ => throw new InvalidOperationException("Rejected recipient jane.doe@example.com"));

        var job = await RunAsync(database, jobId, ProviderFor(JobType.InvitationEmail, handler));

        Assert.NotNull(job.ErrorMessage);
        Assert.DoesNotContain("jane.doe@example.com", job.ErrorMessage);
        Assert.Contains("Rejected recipient", job.ErrorMessage);
    }

    /// <summary>
    /// A job type nobody registered a handler for must say so by name. Letting DI throw instead
    /// would report a missing service and leave the operator to work out which type it meant.
    /// </summary>
    [Fact]
    public async Task AJobTypeWithNoRegisteredHandlerFailsByName()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database, JobType.ReportEmail);

        var job = await RunAsync(database, jobId, ProviderWithNoHandlers());

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Contains("Unknown job type", job.ErrorMessage!);
        Assert.Contains(nameof(JobType.ReportEmail), job.ErrorMessage!);
    }

    /// <summary>
    /// Handlers are resolved by key, so a handler registered for one type must not be reached by
    /// another. A mix-up here would send the wrong participant the wrong email.
    /// </summary>
    [Fact]
    public async Task AHandlerRegisteredForAnotherTypeIsNotInvoked()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database, JobType.ResultEmail);
        var handler = new StubHandler(_ => Task.CompletedTask);

        var job = await RunAsync(database, jobId, ProviderFor(JobType.InvitationEmail, handler));

        Assert.False(handler.WasCalled);
        Assert.NotEqual(JobStatus.Completed, job.Status);
    }

    /// <summary>
    /// A job cancelled while a worker was already holding it must stay cancelled. Returning it to
    /// Pending would leave a runnable job whose payload Cancel() has already cleared.
    /// </summary>
    [Fact]
    public async Task AJobCancelledMidFlightIsNotReturnedToTheQueue()
    {
        using var database = SqliteTestDatabase.Create();
        var jobId = await SeedClaimedJobAsync(database);

        await using (var cancelContext = database.CreateContext())
        {
            var toCancel = await cancelContext.Jobs.FindAsync([jobId], TestContext.Current.CancellationToken);
            toCancel!.Cancel("Participant withdrew consent");
            await cancelContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var handler = new StubHandler(_ => throw new InvalidOperationException("SMTP timed out"));
        var job = await RunAsync(database, jobId, ProviderFor(JobType.InvitationEmail, handler));

        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.Equal(0, job.Attempts);
    }
}
